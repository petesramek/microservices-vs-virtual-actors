using Comparison.Contracts;
using Inventory.Api.Data;
using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddDbContext<InventoryDbContext>(options => {
    var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=inventory.db";
    options.UseSqlite(connectionString);
});

var app = builder.Build();
// correlation-id-logging
app.Use(async (context, next) => {
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId)) {
        await next().ConfigureAwait(false);
        return;
    }

    using var scope = app.Logger.BeginScope(new Dictionary<string, object> {
        ["CorrelationId"] = correlationId,
    });

    if (app.Logger.IsEnabled(LogLevel.Information)) {
        app.Logger.LogInformation("Handling request with correlation id {CorrelationId}.", correlationId);
    }
    await next().ConfigureAwait(false);
});

await EnsureDatabaseAsync(app.Services).ConfigureAwait(false);

app.MapGet("/", () => Results.Ok(new { Name = "Inventory API", Phase = "Microservices" }));
app.MapGet("/health/live", () => Results.Ok("Healthy"));

app.MapPost("/api/inventory/reset", async (ResetInventoryRequest request, InventoryDbContext db, CancellationToken cancellationToken) => {
    var item = await db.Items.SingleOrDefaultAsync(x => x.ProductId == request.ProductId, cancellationToken).ConfigureAwait(false);
    if (item is null) {
        item = new InventoryItem { ProductId = request.ProductId };
        db.Items.Add(item);
    }

    item.AvailableQuantity = request.Quantity;

    var reservations = await db.Reservations.Where(x => x.ProductId == request.ProductId).ToListAsync(cancellationToken).ConfigureAwait(false);
    db.Reservations.RemoveRange(reservations);

    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    return Results.Ok(new InventoryResponse(item.ProductId, item.AvailableQuantity));
});

app.MapGet("/api/inventory/{productId}", async (string productId, InventoryDbContext db, CancellationToken cancellationToken) => {
    var item = await db.Items.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == productId, cancellationToken).ConfigureAwait(false);
    return item is null
        ? Results.Ok(new InventoryResponse(productId, 0))
        : Results.Ok(new InventoryResponse(item.ProductId, item.AvailableQuantity));
});

app.MapPost("/api/inventory/{productId}/reserve", async (string productId, ReserveInventoryRequest request, InventoryDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken) => {
    var logger = loggerFactory.CreateLogger("Inventory.Reserve");

    var existingReservation = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(x => x.ReservationId == request.ReservationId, cancellationToken).ConfigureAwait(false);
    if (existingReservation is not null) {
        var current = await db.Items.AsNoTracking().SingleAsync(x => x.ProductId == productId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new ReserveInventoryResponse(true, null, current.AvailableQuantity));
    }

    var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    await using (transaction.ConfigureAwait(false)) {
        var item = await db.Items.SingleOrDefaultAsync(x => x.ProductId == productId, cancellationToken).ConfigureAwait(false);

    if (item is null || item.AvailableQuantity < request.Quantity) {
        var availableQuantity = item?.AvailableQuantity ?? 0;
        return Results.Ok(new ReserveInventoryResponse(false, "InsufficientInventory", availableQuantity));
    }

    item.AvailableQuantity -= request.Quantity;
    db.Reservations.Add(new InventoryReservation {
        ReservationId = request.ReservationId,
        OrderId = request.OrderId,
        ProductId = productId,
        Quantity = request.Quantity,
    });

    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

    if (logger.IsEnabled(LogLevel.Information)) {
        logger.LogInformation("Reserved {Quantity} item(s) of product {ProductId} for order {OrderId}", request.Quantity, productId, request.OrderId);
    }
    return Results.Ok(new ReserveInventoryResponse(true, null, item.AvailableQuantity));
    }
});

app.MapPost("/api/inventory/{productId}/release", async (string productId, ReleaseInventoryRequest request, InventoryDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken) => {
    var logger = loggerFactory.CreateLogger("Inventory.Release");
    var reservation = await db.Reservations.SingleOrDefaultAsync(x => x.ReservationId == request.ReservationId && x.ProductId == productId, cancellationToken).ConfigureAwait(false);

    if (reservation is null) {
        var current = await db.Items.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == productId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new InventoryResponse(productId, current?.AvailableQuantity ?? 0));
    }

    var item = await db.Items.SingleAsync(x => x.ProductId == productId, cancellationToken).ConfigureAwait(false);
    item.AvailableQuantity += reservation.Quantity;
    db.Reservations.Remove(reservation);
    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    if (logger.IsEnabled(LogLevel.Information)) {
        logger.LogInformation("Released reservation {ReservationId} for product {ProductId}", request.ReservationId, productId);
    }
    return Results.Ok(new InventoryResponse(productId, item.AvailableQuantity));
});

app.Run();

static async Task EnsureDatabaseAsync(IServiceProvider services) {
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
}

public partial class Program;

