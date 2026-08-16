namespace Ordering.Api.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Ordering.Api.Internal.Observability.Logging;
using Ordering.Api.Logging;
using Ordering.Grains.Contracts;
using Ordering.Grains.Grains.Abstraction;
using Orleans;
using Workbench.Contracts.Inventory;
using Workbench.Contracts.Orders;
using Workbench.Contracts.Scenarios;

/// <summary>
/// Provides endpoint-registration methods for the ordering API.
/// </summary>
internal static class EndpointRouteBuilderExtensions {
    /// <summary>
    /// Maps the ordering API root, inventory, and order endpoints.
    /// </summary>
    /// <param name="endpoints">
    /// The endpoint route builder that receives the ordering routes.
    /// </param>
    /// <returns>
    /// <paramref name="endpoints"/> so additional endpoints can be mapped.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="endpoints"/> is <see langword="null"/>.
    /// </exception>
    public static IEndpointRouteBuilder MapOrderingEndpoints(
        this IEndpointRouteBuilder endpoints) {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/", GetServiceInformation);

        RouteGroupBuilder api = endpoints.MapGroup("/api");

        api.MapPost("/scenarios/reset", ResetInventoryAsync);
        api.MapGet("/inventory/{productId}", GetInventoryAsync);
        api.MapPost("/orders", PlaceOrderAsync);
        api.MapGet("/orders/{orderId:guid}", GetOrderAsync);

        return endpoints;
    }

    /// <summary>
    /// Returns identifying information for the ordering API.
    /// </summary>
    /// <returns>An HTTP 200 response containing the service name and phase.</returns>
    private static IResult GetServiceInformation() {
        return Results.Ok(new {
            Name = "Ordering API",
            Phase = "Virtual Actors",
        });
    }

    /// <summary>
    /// Resets inventory for a product through its inventory grain.
    /// </summary>
    /// <param name="request">The inventory-reset request.</param>
    /// <param name="client">The Orleans cluster client.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>An HTTP result describing the reset outcome.</returns>
    private static async Task<IResult> ResetInventoryAsync(
        ResetInventoryRequest request,
        IClusterClient client,
        ILoggerFactory loggerFactory) {
        ILogger logger =
            loggerFactory.CreateLogger("Ordering.ResetInventory");
        IInventoryItemGrain inventory =
            client.GetGrain<IInventoryItemGrain>(request.ProductId);

        logger.ResettingInventory(request.ProductId, request.Quantity);

        try {
            InventorySnapshot snapshot = await inventory
                .ResetAsync(request.Quantity)
                .ConfigureAwait(false);

            logger.InventoryReset(
                snapshot.ProductId,
                snapshot.AvailableQuantity);

            return Results.Ok(new InventoryResponse(
                snapshot.ProductId,
                snapshot.AvailableQuantity));
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
    /// Retrieves the current inventory snapshot for a product.
    /// </summary>
    /// <param name="productId">The product identifier.</param>
    /// <param name="client">The Orleans cluster client.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>An HTTP result containing the inventory snapshot.</returns>
    private static async Task<IResult> GetInventoryAsync(
        string productId,
        IClusterClient client,
        ILoggerFactory loggerFactory) {
        ILogger logger = loggerFactory.CreateLogger("Ordering.GetInventory");
        IInventoryItemGrain inventory =
            client.GetGrain<IInventoryItemGrain>(productId);

        try {
            InventorySnapshot snapshot = await inventory
                .GetAsync()
                .ConfigureAwait(false);

            logger.InventoryRetrieved(
                snapshot.ProductId,
                snapshot.AvailableQuantity);

            return Results.Ok(new InventoryResponse(
                snapshot.ProductId,
                snapshot.AvailableQuantity));
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.InventoryRetrievalFailed(exception, productId);

            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Places an order through its order grain.
    /// </summary>
    /// <param name="request">The order scenario request.</param>
    /// <param name="client">The Orleans cluster client.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>An HTTP result describing the order outcome.</returns>
    private static async Task<IResult> PlaceOrderAsync(
        RunScenarioRequest request,
        IClusterClient client,
        ILoggerFactory loggerFactory) {
        ILogger logger = loggerFactory.CreateLogger("Ordering.PlaceOrder");
        IOrderGrain order = client.GetGrain<IOrderGrain>(request.OrderId);

        logger.PlacingOrder(
            request.OrderId,
            request.CustomerId,
            request.ProductId,
            request.Quantity);

        try {
            GrainOrderResult result = await order
                .PlaceAsync(
                    request.IdempotencyKey,
                    request.CustomerId,
                    request.ProductId,
                    request.Quantity,
                    request.SimulatePaymentFailure)
                .ConfigureAwait(false);

            logger.OrderCompletedWithStatus(result.OrderId, result.Status);

            return Results.Ok(ToResponse(result));
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
    /// Retrieves the current result for an order.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="client">The Orleans cluster client.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>
    /// An HTTP 200 response when the order exists, HTTP 404 when no result is
    /// available, or HTTP 500 when retrieval fails.
    /// </returns>
    private static async Task<IResult> GetOrderAsync(
        Guid orderId,
        IClusterClient client,
        ILoggerFactory loggerFactory) {
        ILogger logger = loggerFactory.CreateLogger("Ordering.GetOrder");
        IOrderGrain order = client.GetGrain<IOrderGrain>(orderId);

        try {
            GrainOrderResult? result = await order
                .GetAsync()
                .ConfigureAwait(false);

            if (result is null) {
                logger.OrderNotFound(orderId);
                return Results.NotFound();
            }

            logger.OrderRetrievedWithStatus(result.OrderId, result.Status);

            return Results.Ok(ToResponse(result));
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.OrderRetrievalFailed(exception, orderId);

            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Converts a grain order result to the public workbench response contract.
    /// </summary>
    /// <param name="result">The grain order result to convert.</param>
    /// <returns>The corresponding order response.</returns>
    private static OrderResponse ToResponse(GrainOrderResult result) {
        return new OrderResponse(
            result.OrderId,
            Enum.Parse<OrderStatus>(result.Status),
            result.Reason);
    }
}
