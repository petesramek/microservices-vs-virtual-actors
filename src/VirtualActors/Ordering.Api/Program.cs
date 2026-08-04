using Comparison.Contracts;
using Hosting.ServiceDefaults.Extensions;
using Ordering.Api.HealthChecks;
using Ordering.Api.Logging;
using Ordering.Grains.Contracts;
using Ordering.Grains.Interfaces;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add shared Aspire service discovery, resilience, health checks, and OpenTelemetry.
builder.AddServiceDefaults();

builder.UseOrleansClient(clientBuilder => {
    clientBuilder
        .UseLocalhostClustering()
        .AddActivityPropagation();
});

builder.Services
    .AddHealthChecks()
    .AddCheck<OrleansClusterHealthCheck>("orleans-cluster");

WebApplication app = builder.Build();

// correlation-id-logging
app.Use(async (context, next) => {
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();

    if (string.IsNullOrWhiteSpace(correlationId)) {
        await next().ConfigureAwait(false);
        return;
    }

    using IDisposable? scope = app.Logger.BeginScope(
        new Dictionary<string, object>(StringComparer.Ordinal) {
            ["CorrelationId"] = correlationId,
        });

    app.Logger.HandlingRequestWithCorrelationId(correlationId);

    await next().ConfigureAwait(false);
});

app.MapGet("/", () => Results.Ok(new {
    Name = "Ordering API",
    Phase = "Virtual Actors",
}));

app.MapPost("/api/scenarios/reset", async (
    ResetInventoryRequest request,
    IClusterClient client,
    ILoggerFactory loggerFactory) => {
        ILogger logger = loggerFactory.CreateLogger("Ordering.ResetInventory");
        IInventoryItemGrain inventory = client.GetGrain<IInventoryItemGrain>(request.ProductId);

        logger.ResettingInventory(request.ProductId, request.Quantity);

        try {
            InventorySnapshot snapshot = await inventory
                .ResetAsync(request.Quantity)
                .ConfigureAwait(false);

            logger.InventoryReset(snapshot.ProductId, snapshot.AvailableQuantity);

            return Results.Ok(new InventoryResponse(
                snapshot.ProductId,
                snapshot.AvailableQuantity));
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception exception) {
            logger.InventoryResetFailed(
                exception,
                request.ProductId,
                request.Quantity);

            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError);
        }
    });

app.MapGet("/api/inventory/{productId}", async (
    string productId,
    IClusterClient client,
    ILoggerFactory loggerFactory) => {
        ILogger logger = loggerFactory.CreateLogger("Ordering.GetInventory");
        IInventoryItemGrain inventory = client.GetGrain<IInventoryItemGrain>(productId);

        try {
            InventorySnapshot snapshot = await inventory
                .GetAsync()
                .ConfigureAwait(false);

            logger.InventoryRetrieved(snapshot.ProductId, snapshot.AvailableQuantity);

            return Results.Ok(new InventoryResponse(
                snapshot.ProductId,
                snapshot.AvailableQuantity));
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception exception) {
            logger.InventoryRetrievalFailed(exception, productId);

            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError);
        }
    });

app.MapPost("/api/orders", async (
    RunScenarioRequest request,
    IClusterClient client,
    ILoggerFactory loggerFactory) => {
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
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception exception) {
            logger.OrderPlacementFailed(
                exception,
                request.OrderId,
                request.ProductId,
                request.Quantity);

            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError);
        }
    });

app.MapGet("/api/orders/{orderId:guid}", async (
    Guid orderId,
    IClusterClient client,
    ILoggerFactory loggerFactory) => {
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
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception exception) {
            logger.OrderRetrievalFailed(exception, orderId);

            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError);
        }
    });

// Map the shared health and aliveness endpoints.
app.MapDefaultEndpoints();

await app.RunAsync().ConfigureAwait(false);

static OrderResponse ToResponse(GrainOrderResult result) {
    return new OrderResponse(
        result.OrderId,
        Enum.Parse<OrderStatus>(result.Status),
        result.Reason);
}

public partial class Program;
