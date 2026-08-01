using Comparison.Contracts;
using Ordering.Api.Logging;
using Ordering.Grains.Contracts;
using Ordering.Grains.Interfaces;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Host.UseOrleans(siloBuilder => {
    siloBuilder.UseLocalhostClustering();
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

app.MapGet("/", () => Results.Ok(new { Name = $"Ordering API", Phase = $"Virtual Actors" }));
app.MapGet("/health/live", () => Results.Ok($"Healthy"));

app.MapPost("/api/scenarios/reset", async (
    ResetInventoryRequest request,
    IClusterClient client,
    ILoggerFactory loggerFactory) => {
    ILogger logger = loggerFactory.CreateLogger("Ordering.ResetInventory");
    IInventoryItemGrain inventory = client.GetGrain<IInventoryItemGrain>(request.ProductId);

    logger.ResettingInventory(request.ProductId, request.Quantity);

    InventorySnapshot snapshot = await inventory.ResetAsync(request.Quantity).ConfigureAwait(false);

    logger.InventoryReset(snapshot.ProductId, snapshot.AvailableQuantity);

    return Results.Ok(new InventoryResponse(snapshot.ProductId, snapshot.AvailableQuantity));
});

app.MapGet("/api/inventory/{productId}", async (
    string productId,
    IClusterClient client,
    ILoggerFactory loggerFactory) => {
    ILogger logger = loggerFactory.CreateLogger("Ordering.GetInventory");
    IInventoryItemGrain inventory = client.GetGrain<IInventoryItemGrain>(productId);
    InventorySnapshot snapshot = await inventory.GetAsync().ConfigureAwait(false);

    logger.InventoryRetrieved(snapshot.ProductId, snapshot.AvailableQuantity);

    return Results.Ok(new InventoryResponse(snapshot.ProductId, snapshot.AvailableQuantity));
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

    GrainOrderResult result = await order.PlaceAsync(
        request.IdempotencyKey,
        request.CustomerId,
        request.ProductId,
        request.Quantity,
        request.SimulatePaymentFailure).ConfigureAwait(false);

    logger.OrderCompletedWithStatus(result.OrderId, result.Status);

    return Results.Ok(ToResponse(result));
});

app.MapGet("/api/orders/{orderId:guid}", async (
    Guid orderId,
    IClusterClient client,
    ILoggerFactory loggerFactory) => {
    ILogger logger = loggerFactory.CreateLogger("Ordering.GetOrder");
    IOrderGrain order = client.GetGrain<IOrderGrain>(orderId);
    GrainOrderResult? result = await order.GetAsync().ConfigureAwait(false);

    if (result is null) {
        return Results.NotFound();
    }

    logger.OrderRetrievedWithStatus(result.OrderId, result.Status);

    return Results.Ok(ToResponse(result));
});

await app.RunAsync().ConfigureAwait(false);

static OrderResponse ToResponse(Ordering.Grains.Contracts.GrainOrderResult result) {
    return new OrderResponse(
        result.OrderId,
        Enum.Parse<OrderStatus>(result.Status),
        result.Reason);
}

public partial class Program;
