using Comparison.Contracts;
using Ordering.Grains.Interfaces;
using Orleans;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Host.UseOrleans(siloBuilder => {
    siloBuilder.UseLocalhostClustering();
});

var app = builder.Build();
// correlation-id-logging
app.Use(async (context, next) => {
    var correlationId = context.Request.Headers[$"X-Correlation-ID"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId)) {
        await next().ConfigureAwait(false);
        return;
    }

    using var scope = app.Logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal) {
        [$"CorrelationId"] = correlationId,
    });

    if (app.Logger.IsEnabled(LogLevel.Information)) {
        app.Logger.LogInformation($"Handling request with correlation id {correlationId}.");
    }
    await next().ConfigureAwait(false);
});

app.MapGet("/", () => Results.Ok(new { Name = $"Ordering API", Phase = $"Virtual Actors"}));
app.MapGet("/health/live", () => Results.Ok($"Healthy"));

app.MapPost("/api/scenarios/reset", async (ResetInventoryRequest request, IClusterClient client) => {
    var inventory = client.GetGrain<IInventoryItemGrain>(request.ProductId);
    var snapshot = await inventory.ResetAsync(request.Quantity).ConfigureAwait(false);
    return Results.Ok(new InventoryResponse(snapshot.ProductId, snapshot.AvailableQuantity));
});

app.MapGet("/api/inventory/{productId}", async (string productId, IClusterClient client) => {
    var inventory = client.GetGrain<IInventoryItemGrain>(productId);
    var snapshot = await inventory.GetAsync().ConfigureAwait(false);
    return Results.Ok(new InventoryResponse(snapshot.ProductId, snapshot.AvailableQuantity));
});

app.MapPost("/api/orders", async (RunScenarioRequest request, IClusterClient client, ILoggerFactory loggerFactory) => {
    var logger = loggerFactory.CreateLogger($"Ordering.PlaceOrder");
    var order = client.GetGrain<IOrderGrain>(request.OrderId);

    var result = await order.PlaceAsync(
        request.IdempotencyKey,
        request.CustomerId,
        request.ProductId,
        request.Quantity,
        request.SimulatePaymentFailure).ConfigureAwait(false);

    if (logger.IsEnabled(LogLevel.Information)) {
        logger.LogInformation($"Virtual actor order {result.OrderId} completed with status {result.Status}");
    }
    return Results.Ok(ToResponse(result));
});

app.MapGet("/api/orders/{orderId:guid}", async (Guid orderId, IClusterClient client) => {
    var order = client.GetGrain<IOrderGrain>(orderId);
    var result = await order.GetAsync().ConfigureAwait(false);
    return result is null ? Results.NotFound() : Results.Ok(ToResponse(result));
});

app.Run();

static OrderResponse ToResponse(Ordering.Grains.Contracts.GrainOrderResult result) {
    return new OrderResponse(
        result.OrderId,
        Enum.Parse<OrderStatus>(result.Status),
        result.Reason);
}

public partial class Program;

