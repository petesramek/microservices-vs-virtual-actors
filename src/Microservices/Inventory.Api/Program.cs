using Comparison.Contracts;
using Inventory.Api.Data;
using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddDbContext<InventoryDbContext>(options => {
    var connectionString = builder.Configuration.GetConnectionString($"Default") ?? $"Data Source=inventory.db";
    options.UseSqlite(connectionString);
});

WebApplication app = builder.Build();
// correlation-id-logging
app.Use(async (context, next) => {
    var correlationId = context.Request.Headers[$"X-Correlation-ID"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId)) {
        await next().ConfigureAwait(false);
        return;
    }

    using IDisposable? scope = app.Logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal) {
        [$"CorrelationId"] = correlationId,
    });

    if (app.Logger.IsEnabled(LogLevel.Information)) {
        app.Logger.LogInformation($"Handling request with correlation id {correlationId}.");
    }
    await next().ConfigureAwait(false);
});

await EnsureDatabaseAsync(app.Services).ConfigureAwait(false);

app.MapGet($"/", () => Results.Ok(new { Name = $"Inventory API", Phase = $"Microservices" }));
app.MapGet($"/health/live", () => Results.Ok($"Healthy"));

app.MapPost($"/api/inventory/reset", async (ResetInventoryRequest request, InventoryDbContext db, CancellationToken cancellationToken) => {
    InventoryItem? item = await db.Items.SingleOrDefaultAsync(x => x.ProductId == request.ProductId, cancellationToken).ConfigureAwait(false);
    if (item is null) {
        item = new InventoryItem { ProductId = request.ProductId };
        db.Items.Add(item);
    }

    item.AvailableQuantity = request.Quantity;

    List<InventoryReservation> reservations = await db.Reservations.Where(x => x.ProductId == request.ProductId).ToListAsync(cancellationToken).ConfigureAwait(false);
    db.Reservations.RemoveRange(reservations);

    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    return Results.Ok(new InventoryResponse(item.ProductId, item.AvailableQuantity));
});

app.MapGet("/api/inventory/{productId}", async (string productId, InventoryDbContext db, CancellationToken cancellationToken) => {
    InventoryItem? item = await db.Items.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == productId, cancellationToken).ConfigureAwait(false);
    return item is null
        ? Results.Ok(new InventoryResponse(productId, 0))
        : Results.Ok(new InventoryResponse(item.ProductId, item.AvailableQuantity));
});

app.MapPost("/api/inventory/{productId}/reserve", async (string productId, ReserveInventoryRequest request, InventoryDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken) => {
    ILogger logger = loggerFactory.CreateLogger($"Inventory.Reserve");

    InventoryReservation? existingReservation = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(x => x.ReservationId == request.ReservationId, cancellationToken).ConfigureAwait(false);
    if (existingReservation is not null) {
        InventoryItem current = await db.Items.AsNoTracking().SingleAsync(x => x.ProductId == productId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new ReserveInventoryResponse(Reserved: true, Reason: null, current.AvailableQuantity));
    }

    IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    await using (transaction.ConfigureAwait(false)) {
        InventoryItem? item = await db.Items.SingleOrDefaultAsync(x => x.ProductId == productId, cancellationToken).ConfigureAwait(false);

        if (item is null || item.AvailableQuantity < request.Quantity) {
            var availableQuantity = item?.AvailableQuantity ?? 0;
            return Results.Ok(new ReserveInventoryResponse(Reserved: false, $"InsufficientInventory", availableQuantity));
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
            logger.LogInformation($"Reserved {request.Quantity} item(s) of product {productId} for order {request.OrderId}");
        }
        return Results.Ok(new ReserveInventoryResponse(Reserved: true, Reason: null, item.AvailableQuantity));
    }
});

app.MapPost("/api/inventory/{productId}/release", async (string productId, ReleaseInventoryRequest request, InventoryDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken) => {
    ILogger logger = loggerFactory.CreateLogger($"Inventory.Release");
    InventoryReservation? reservation = await db.Reservations.SingleOrDefaultAsync(x => x.ReservationId == request.ReservationId && x.ProductId == productId, cancellationToken).ConfigureAwait(false);

    if (reservation is null) {
        InventoryItem? current = await db.Items.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == productId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new InventoryResponse(productId, current?.AvailableQuantity ?? 0));
    }

    InventoryItem item = await db.Items.SingleAsync(x => x.ProductId == productId, cancellationToken).ConfigureAwait(false);
    item.AvailableQuantity += reservation.Quantity;
    db.Reservations.Remove(reservation);
    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    if (logger.IsEnabled(LogLevel.Information)) {
        logger.LogInformation($"Released reservation {request.ReservationId} for product {productId}");
    }
    return Results.Ok(new InventoryResponse(productId, item.AvailableQuantity));
});
await app.RunAsync().ConfigureAwait(false);

static async Task EnsureDatabaseAsync(IServiceProvider services) {
    using IServiceScope scope = services.CreateScope();
    InventoryDbContext db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
}

public partial class Program;

