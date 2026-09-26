namespace Inventory.Api.Extensions;

using Inventory.Api.Internal.Infrastructure;
using Inventory.Api.Internal.Observability.Logging;
using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Workbench.Contracts.Inventory;

/// <summary>
/// Provides endpoint mappings for the inventory service.
/// </summary>
internal static class InventoryEndpointRouteBuilderExtensions {
    /// <summary>
    /// Maps the inventory service information, inventory operations, and shared
    /// health endpoints.
    /// </summary>
    /// <param name="endpoints">
    /// The endpoint route builder that receives the inventory routes.
    /// </param>
    /// <returns>The supplied endpoint route builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="endpoints"/> is <see langword="null"/>.
    /// </exception>
    internal static IEndpointRouteBuilder MapInventoryEndpoints(
        this IEndpointRouteBuilder endpoints) {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/", () => Results.Ok(new {
            Name = "Inventory API",
            Phase = "Microservices",
        }));

        endpoints.MapPost("/api/inventory/reset", ResetInventoryAsync);
        endpoints.MapGet("/api/inventory/{productId}", GetInventoryAsync);
        endpoints.MapPost(
            "/api/inventory/{productId}/reserve",
            ReserveInventoryAsync);
        endpoints.MapPost(
            "/api/inventory/{productId}/release",
            ReleaseInventoryAsync);

        return endpoints;
    }

    /// <summary>
    /// Resets one product's available quantity and removes its reservations.
    /// </summary>
    /// <param name="request">The requested inventory reset.</param>
    /// <param name="db">The inventory database context.</param>
    /// <param name="loggerFactory">The factory used to create the endpoint logger.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The resulting inventory response.</returns>
    private static async Task<IResult> ResetInventoryAsync(
        ResetInventoryRequest request,
        InventoryDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) {
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
                .Where(reservation =>
                    reservation.ProductId == request.ProductId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            db.Reservations.RemoveRange(reservations);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            logger.InventoryReset(item.ProductId, item.AvailableQuantity);

            return Results.Ok(new InventoryResponse(
                item.ProductId,
                item.AvailableQuantity));
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.InventoryResetFailed(
                exception,
                request.ProductId,
                request.Quantity);

            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets the current available quantity for one product.
    /// </summary>
    /// <param name="productId">The product identifier.</param>
    /// <param name="db">The inventory database context.</param>
    /// <param name="loggerFactory">The factory used to create the endpoint logger.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The current inventory response.</returns>
    private static async Task<IResult> GetInventoryAsync(
        string productId,
        InventoryDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) {
        ILogger logger = loggerFactory.CreateLogger("Inventory.Get");

        try {
            InventoryItem? item = await db.Items
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.ProductId == productId,
                    cancellationToken)
                .ConfigureAwait(false);

            InventoryResponse response = item is null
                ? new InventoryResponse(productId, 0)
                : new InventoryResponse(
                    item.ProductId,
                    item.AvailableQuantity);

            logger.InventoryRetrieved(
                response.ProductId,
                response.AvailableQuantity);

            return Results.Ok(response);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.InventoryRetrievalFailed(exception, productId);

            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Attempts to reserve inventory for an order.
    /// </summary>
    /// <param name="productId">The product identifier.</param>
    /// <param name="request">The requested reservation.</param>
    /// <param name="db">The inventory database context.</param>
    /// <param name="loggerFactory">The factory used to create the endpoint logger.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The reservation result.</returns>
    private static async Task<IResult> ReserveInventoryAsync(
        string productId,
        ReserveInventoryRequest request,
        InventoryDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) {
        ILogger logger = loggerFactory.CreateLogger("Inventory.Reserve");
        logger.ReservingInventory(
            productId,
            request.OrderId,
            request.ReservationId,
            request.Quantity);

        try {
            InventoryReservation? existingReservation = await db.Reservations
                .AsNoTracking()
                .SingleOrDefaultAsync(reservation => reservation.ReservationId == request.ReservationId, cancellationToken)
                .ConfigureAwait(false);

            if (existingReservation is not null) {
                InventoryItem current = await db.Items
                    .AsNoTracking()
                    .SingleAsync(item => item.ProductId == productId, cancellationToken)
                    .ConfigureAwait(false);

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

            IDbContextTransaction transaction = await db.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            await using (transaction.ConfigureAwait(false)) {
                InventoryItem? item = await db.Items
                    .SingleOrDefaultAsync(item => item.ProductId == productId, cancellationToken)
                    .ConfigureAwait(false);

                if (item is null || item.AvailableQuantity < request.Quantity) {
                    int availableQuantity = item?.AvailableQuantity ?? 0;

                    logger.InventoryReservationCompleted(
                        productId,
                        request.OrderId,
                        request.ReservationId,
                        reserved: false,
                        availableQuantity);

                    return Results.Ok(new ReserveInventoryResponse(
                        Reserved: false,
                        "InsufficientInventory",
                        availableQuantity));
                }

                item.AvailableQuantity -= request.Quantity;

                db.Reservations.Add(new InventoryReservation {
                    ReservationId = request.ReservationId,
                    OrderId = request.OrderId,
                    ProductId = productId,
                    Quantity = request.Quantity,
                });

                await db.SaveChangesAsync(cancellationToken)
                    .ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken)
                    .ConfigureAwait(false);

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
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.InventoryReservationFailed(
                exception,
                productId,
                request.OrderId,
                request.ReservationId,
                request.Quantity);

            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Releases an inventory reservation and restores its quantity.
    /// </summary>
    /// <param name="productId">The product identifier.</param>
    /// <param name="request">The requested reservation release.</param>
    /// <param name="db">The inventory database context.</param>
    /// <param name="loggerFactory">The factory used to create the endpoint logger.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The resulting inventory response.</returns>
    private static async Task<IResult> ReleaseInventoryAsync(
        string productId,
        ReleaseInventoryRequest request,
        InventoryDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) {
        ILogger logger = loggerFactory.CreateLogger("Inventory.Release");
        logger.ReleasingInventory(productId, request.ReservationId);

        try {
            InventoryReservation? reservation = await db.Reservations
                .SingleOrDefaultAsync(
                    reservation =>
                        reservation.ReservationId == request.ReservationId
                        && reservation.ProductId == productId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (reservation is null) {
                InventoryItem? current = await db.Items
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        item => item.ProductId == productId,
                        cancellationToken)
                    .ConfigureAwait(false);

                int availableQuantity = current?.AvailableQuantity ?? 0;

                logger.InventoryReleased(
                    productId,
                    request.ReservationId,
                    availableQuantity);

                return Results.Ok(new InventoryResponse(
                    productId,
                    availableQuantity));
            }

            InventoryItem item = await db.Items.SingleAsync(
                item => item.ProductId == productId,
                cancellationToken).ConfigureAwait(false);

            item.AvailableQuantity += reservation.Quantity;
            db.Reservations.Remove(reservation);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            logger.InventoryReleased(
                productId,
                request.ReservationId,
                item.AvailableQuantity);

            return Results.Ok(new InventoryResponse(
                productId,
                item.AvailableQuantity));
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.InventoryReleaseFailed(
                exception,
                productId,
                request.ReservationId);

            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
