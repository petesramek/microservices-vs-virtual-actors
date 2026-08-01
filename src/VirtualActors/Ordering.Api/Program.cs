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
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId)) {
        await next();
        return;
    }

    using var scope = app.Logger.BeginScope(new Dictionary<string, object> {
        ["CorrelationId"] = correlationId
    });

    app.Logger.LogInformation("Handling request with correlation id {CorrelationId}.", correlationId);
    await next();
});

app.MapGet("/", () => Results.Ok(new { Name = "Ordering API", Phase = "Virtual Actors" }));
app.MapGet("/health/live", () => Results.Ok("Healthy"));

app.MapPost("/api/scenarios/reset", async (ResetInventoryRequest request, IClusterClient client) => {
    var inventory = client.GetGrain<IInventoryItemGrain>(request.ProductId);
    var snapshot = await inventory.ResetAsync(request.Quantity);
    return Results.Ok(new InventoryResponse(snapshot.ProductId, snapshot.AvailableQuantity));
});

app.MapGet("/api/inventory/{productId}", async (string productId, IClusterClient client) => {
    var inventory = client.GetGrain<IInventoryItemGrain>(productId);
    var snapshot = await inventory.GetAsync();
    return Results.Ok(new InventoryResponse(snapshot.ProductId, snapshot.AvailableQuantity));
});

app.MapPost("/api/orders", async (RunScenarioRequest request, IClusterClient client, ILoggerFactory loggerFactory) => {
    var logger = loggerFactory.CreateLogger("Ordering.PlaceOrder");
    var order = client.GetGrain<IOrderGrain>(request.OrderId);

    var result = await order.PlaceAsync(
        request.IdempotencyKey,
        request.CustomerId,
        request.ProductId,
        request.Quantity,
        request.SimulatePaymentFailure);

    logger.LogInformation("Virtual actor order {OrderId} completed with status {Status}", result.OrderId, result.Status);
    return Results.Ok(ToResponse(result));
});

app.MapGet("/api/orders/{orderId:guid}", async (Guid orderId, IClusterClient client) => {
    var order = client.GetGrain<IOrderGrain>(orderId);
    var result = await order.GetAsync();
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

