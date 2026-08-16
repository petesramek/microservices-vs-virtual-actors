namespace Orders.Api.Extensions;

using Microsoft.EntityFrameworkCore;
using Orders.Api.Internal.Clients.Abstraction;
using Orders.Api.Internal.Infrastructure;
using Orders.Api.Logging;
using Orders.Api.Models;
using Workbench.Contracts.Inventory;
using Workbench.Contracts.Orders;
using Workbench.Contracts.Payments;
using Workbench.Contracts.Scenarios;

/// <summary>
/// Provides endpoint mappings for the Orders API.
/// </summary>
internal static class OrdersEndpointRouteBuilderExtensions {
    /// <summary>
    /// Maps service information, scenario reset, inventory lookup, order
    /// placement, order retrieval, and shared health endpoints.
    /// </summary>
    /// <param name="endpoints">
    /// The endpoint route builder that receives the Orders API routes.
    /// </param>
    /// <returns>The supplied endpoint route builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="endpoints"/> is <see langword="null"/>.
    /// </exception>
    internal static IEndpointRouteBuilder MapOrdersEndpoints(
        this IEndpointRouteBuilder endpoints) {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/", () => Results.Ok(new {
            Name = "Orders API",
            Phase = "Microservices",
        }));

        endpoints.MapPost("/api/scenarios/reset", ResetScenarioAsync);
        endpoints.MapGet("/api/inventory/{productId}", GetInventoryAsync);
        endpoints.MapPost("/api/orders", PlaceOrderAsync);
        endpoints.MapGet("/api/orders/{orderId:guid}", GetOrderAsync);

        return endpoints;
    }

    /// <summary>
    /// Clears persisted orders and resets inventory for the selected product.
    /// </summary>
    /// <param name="request">The requested inventory reset.</param>
    /// <param name="inventoryClient">The Inventory API client.</param>
    /// <param name="db">The orders database context.</param>
    /// <param name="loggerFactory">The factory used to create the endpoint logger.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The resulting inventory response.</returns>
    private static async Task<IResult> ResetScenarioAsync(
        ResetInventoryRequest request,
        IInventoryClient inventoryClient,
        OrdersDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) {
        ILogger logger = loggerFactory.CreateLogger("Orders.ResetInventory");
        logger.ResettingInventory(request.ProductId, request.Quantity);

        try {
            db.Orders.RemoveRange(
                await db.Orders
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false));

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            InventoryResponse inventory = await inventoryClient
                .ResetAsync(request, cancellationToken)
                .ConfigureAwait(false);

            logger.InventoryReset(
                inventory.ProductId,
                inventory.AvailableQuantity);

            return Results.Ok(inventory);
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
    /// Retrieves the current available inventory for a product.
    /// </summary>
    /// <param name="productId">The product identifier.</param>
    /// <param name="inventoryClient">The Inventory API client.</param>
    /// <param name="loggerFactory">The factory used to create the endpoint logger.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The current inventory response.</returns>
    private static async Task<IResult> GetInventoryAsync(
        string productId,
        IInventoryClient inventoryClient,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) {
        ILogger logger = loggerFactory.CreateLogger("Orders.GetInventory");

        try {
            InventoryResponse inventory = await inventoryClient
                .GetAsync(productId, cancellationToken)
                .ConfigureAwait(false);

            logger.InventoryRetrieved(
                inventory.ProductId,
                inventory.AvailableQuantity);

            return Results.Ok(inventory);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.InventoryRetrievalFailed(exception, productId);

            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Places an idempotent order by reserving inventory and authorizing payment.
    /// </summary>
    /// <param name="request">The order scenario request.</param>
    /// <param name="db">The orders database context.</param>
    /// <param name="inventoryClient">The Inventory API client.</param>
    /// <param name="paymentsClient">The Payments API client.</param>
    /// <param name="loggerFactory">The factory used to create the endpoint logger.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The persisted order outcome.</returns>
    private static async Task<IResult> PlaceOrderAsync(
        RunScenarioRequest request,
        OrdersDbContext db,
        IInventoryClient inventoryClient,
        IPaymentsClient paymentsClient,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) {
        ILogger logger = loggerFactory.CreateLogger("Orders.PlaceOrder");
        logger.PlacingOrder(
            request.OrderId,
            request.CustomerId,
            request.ProductId,
            request.Quantity);

        try {
            OrderRecord? existing = await db.Orders
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    order => order.IdempotencyKey == request.IdempotencyKey,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null) {
                logger.OrderCompletedWithStatus(
                    existing.OrderId,
                    existing.Status);

                return Results.Ok(ToResponse(existing));
            }

            Guid reservationId = Guid.NewGuid();
            OrderRecord order = new() {
                OrderId = request.OrderId,
                IdempotencyKey = request.IdempotencyKey,
                CustomerId = request.CustomerId,
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                ReservationId = reservationId,
                Status = OrderStatus.Created.ToString(),
            };

            db.Orders.Add(order);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            ReserveInventoryResponse reservation = await inventoryClient
                .ReserveAsync(
                    request.ProductId,
                    new ReserveInventoryRequest(
                        reservationId,
                        request.OrderId,
                        request.Quantity),
                    cancellationToken)
                .ConfigureAwait(false);

            if (!reservation.Reserved) {
                order.Status = OrderStatus.Rejected.ToString();
                order.Reason = reservation.Reason;
                await db.SaveChangesAsync(cancellationToken)
                    .ConfigureAwait(false);

                logger.OrderCompletedWithStatus(order.OrderId, order.Status);
                return Results.Ok(ToResponse(order));
            }

            order.Status = OrderStatus.InventoryReserved.ToString();
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            AuthorizePaymentResponse payment = await paymentsClient
                .AuthorizeAsync(
                    new AuthorizePaymentRequest(
                        Guid.NewGuid(),
                        request.OrderId,
                        request.CustomerId,
                        request.IdempotencyKey,
                        request.SimulatePaymentFailure),
                    cancellationToken)
                .ConfigureAwait(false);

            if (!payment.Authorized) {
                await inventoryClient.ReleaseAsync(
                    request.ProductId,
                    new ReleaseInventoryRequest(reservationId),
                    cancellationToken).ConfigureAwait(false);

                order.Status = OrderStatus.Rejected.ToString();
                order.Reason = payment.Reason;
                await db.SaveChangesAsync(cancellationToken)
                    .ConfigureAwait(false);

                logger.OrderCompletedWithStatus(order.OrderId, order.Status);
                return Results.Ok(ToResponse(order));
            }

            order.Status = OrderStatus.Completed.ToString();
            order.Reason = null;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            logger.OrderCompletedWithStatus(order.OrderId, order.Status);
            return Results.Ok(ToResponse(order));
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.OrderPlacementFailed(
                exception,
                request.OrderId,
                request.ProductId,
                request.Quantity);

            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Retrieves a persisted order by its identifier.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="db">The orders database context.</param>
    /// <param name="loggerFactory">The factory used to create the endpoint logger.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The order response, or HTTP 404 when the order does not exist.</returns>
    private static async Task<IResult> GetOrderAsync(
        Guid orderId,
        OrdersDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) {
        ILogger logger = loggerFactory.CreateLogger("Orders.GetOrder");

        try {
            OrderRecord? order = await db.Orders
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    order => order.OrderId == orderId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (order is null) {
                logger.OrderNotFound(orderId);
                return Results.NotFound();
            }

            logger.OrderRetrievedWithStatus(order.OrderId, order.Status);
            return Results.Ok(ToResponse(order));
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.OrderRetrievalFailed(exception, orderId);

            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Converts a persisted order record to its public response contract.
    /// </summary>
    /// <param name="order">The persisted order record.</param>
    /// <returns>The public order response.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="order"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The persisted status is not a valid <see cref="OrderStatus"/> value.
    /// </exception>
    private static OrderResponse ToResponse(OrderRecord order) {
        ArgumentNullException.ThrowIfNull(order);

        return new OrderResponse(
            order.OrderId,
            Enum.Parse<OrderStatus>(order.Status),
            order.Reason);
    }
}
