using Comparison.Contracts;
using Hosting.ServiceDefaults.Extensions;
using Inventory.Api.Data;
using Inventory.Api.Logging;
using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add shared Aspire service discovery, resilience, health checks, and OpenTelemetry.
builder.AddServiceDefaults();

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

    app.Logger.HandlingRequestWithCorrelationId(correlationId);

    await next().ConfigureAwait(false);
});

await EnsureDatabaseAsync(app.Services).ConfigureAwait(false);

app.MapGet($"/", () => Results.Ok(new { Name = $"Inventory API", Phase = $"Microservices" }));
app.MapGet($"/health/live", () => Results.Ok($"Healthy"));

app.MapPost($"/api/inventory/reset", async (
    ResetInventoryRequest request,
    InventoryDbContext db,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) => {
    ILogger logger = loggerFactory.CreateLogger("Inventory.Reset");

    logger.ResettingInventory(request.ProductId, request.Quantity);

    try {
        InventoryItem? item = await db.Items.SingleOrDefaultAsync(
            item => item.ProductId == request.ProductId,
            cancellationToken).ConfigureAwait(false);

        if (item is null) {
            item = new InventoryItem { ProductId = request.ProductId };
            db.Items.Add(item);
        }

        item.AvailableQuantity = request.Quantity;

        List<InventoryReservation> reservations = await db.Reservations
            .Where(reservation => reservation.ProductId == request.ProductId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        db.Reservations.RemoveRange(reservations);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.InventoryReset(item.ProductId, item.AvailableQuantity);

        return Results.Ok(new InventoryResponse(item.ProductId, item.AvailableQuantity));
    }
    catch (OperationCanceledException) {
        throw;
    }
    catch (Exception exception) {
        logger.InventoryResetFailed(exception, request.ProductId, request.Quantity);

        return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/api/inventory/{productId}", async (
    string productId,
    InventoryDbContext db,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) => {
    ILogger logger = loggerFactory.CreateLogger("Inventory.Get");

    try {
        InventoryItem? item = await db.Items.AsNoTracking().SingleOrDefaultAsync(
            item => item.ProductId == productId,
            cancellationToken).ConfigureAwait(false);

        var response = item is null
            ? new InventoryResponse(productId, 0)
            : new InventoryResponse(item.ProductId, item.AvailableQuantity);

        logger.InventoryRetrieved(response.ProductId, response.AvailableQuantity);

        return Results.Ok(response);
    }
    catch (OperationCanceledException) {
        throw;
    }
    catch (Exception exception) {
        logger.InventoryRetrievalFailed(exception, productId);

        return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/inventory/{productId}/reserve", async (
    string productId,
    ReserveInventoryRequest request,
    InventoryDbContext db,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) => {
    ILogger logger = loggerFactory.CreateLogger("Inventory.Reserve");

    logger.ReservingInventory(
        productId,
        request.OrderId,
        request.ReservationId,
        request.Quantity);

    try {
        InventoryReservation? existingReservation = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(
            reservation => reservation.ReservationId == request.ReservationId,
            cancellationToken).ConfigureAwait(false);

        if (existingReservation is not null) {
            InventoryItem current = await db.Items.AsNoTracking().SingleAsync(
                item => item.ProductId == productId,
                cancellationToken).ConfigureAwait(false);

            logger.InventoryReservationCompleted(
                productId,
                request.OrderId,
                request.ReservationId,
                reserved: true,
                current.AvailableQuantity);

            return Results.Ok(new ReserveInventoryResponse(
                Reserved: true,
                Reason: null,
                current.AvailableQuantity));
        }

        IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false)) {
            InventoryItem? item = await db.Items.SingleOrDefaultAsync(
                item => item.ProductId == productId,
                cancellationToken).ConfigureAwait(false);

            if (item is null || item.AvailableQuantity < request.Quantity) {
                var availableQuantity = item?.AvailableQuantity ?? 0;

                logger.InventoryReservationCompleted(
                    productId,
                    request.OrderId,
                    request.ReservationId,
                    reserved: false,
                    availableQuantity);

                return Results.Ok(new ReserveInventoryResponse(
                    Reserved: false,
                    $"InsufficientInventory",
                    availableQuantity));
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

            logger.InventoryReservationCompleted(
                productId,
                request.OrderId,
                request.ReservationId,
                reserved: true,
                item.AvailableQuantity);

            return Results.Ok(new ReserveInventoryResponse(
                Reserved: true,
                Reason: null,
                item.AvailableQuantity));
        }
    }
    catch (OperationCanceledException) {
        throw;
    }
    catch (Exception exception) {
        logger.InventoryReservationFailed(
            exception,
            productId,
            request.OrderId,
            request.ReservationId,
            request.Quantity);

        return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/inventory/{productId}/release", async (
    string productId,
    ReleaseInventoryRequest request,
    InventoryDbContext db,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) => {
    ILogger logger = loggerFactory.CreateLogger("Inventory.Release");

    logger.ReleasingInventory(productId, request.ReservationId);

    try {
        InventoryReservation? reservation = await db.Reservations.SingleOrDefaultAsync(
            reservation => reservation.ReservationId == request.ReservationId && reservation.ProductId == productId,
            cancellationToken).ConfigureAwait(false);

        if (reservation is null) {
            InventoryItem? current = await db.Items.AsNoTracking().SingleOrDefaultAsync(
                item => item.ProductId == productId,
                cancellationToken).ConfigureAwait(false);
            var availableQuantity = current?.AvailableQuantity ?? 0;

            logger.InventoryReleased(productId, request.ReservationId, availableQuantity);

            return Results.Ok(new InventoryResponse(productId, availableQuantity));
        }

        InventoryItem item = await db.Items.SingleAsync(
            item => item.ProductId == productId,
            cancellationToken).ConfigureAwait(false);

        item.AvailableQuantity += reservation.Quantity;
        db.Reservations.Remove(reservation);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.InventoryReleased(productId, request.ReservationId, item.AvailableQuantity);

        return Results.Ok(new InventoryResponse(productId, item.AvailableQuantity));
    }
    catch (OperationCanceledException) {
        throw;
    }
    catch (Exception exception) {
        logger.InventoryReleaseFailed(exception, productId, request.ReservationId);

        return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
    }
});

// Map the shared health and aliveness endpoints.
app.MapDefaultEndpoints();

await app.RunAsync().ConfigureAwait(false);

static async Task EnsureDatabaseAsync(IServiceProvider services) {
    using IServiceScope scope = services.CreateScope();
    InventoryDbContext db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
}

public partial class Program;
