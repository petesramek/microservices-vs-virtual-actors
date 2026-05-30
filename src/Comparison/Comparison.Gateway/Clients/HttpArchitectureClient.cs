using System.Diagnostics;
using System.Net.Http.Json;
using ArchitectureComparison.Contracts;

namespace Comparison.Gateway.Clients;

/// <summary>
/// Base HTTP implementation for architecture clients.
/// </summary>
/// <param name="httpClient">The HTTP client.</param>
/// <param name="architectureName">The architecture name.</param>
public abstract class HttpArchitectureClient(HttpClient httpClient, string architectureName) : IArchitectureClient
{
    /// <inheritdoc />
    public async Task<ArchitectureRunResult> RunAsync(RunScenarioRequest request, CancellationToken cancellationToken)
    {
        return request.Scenario switch
        {
            ScenarioKind.ConcurrentOrders => await RunConcurrentOrdersAsync(request, cancellationToken),
            ScenarioKind.DuplicateRequest => await RunDuplicateRequestAsync(request, cancellationToken),
            _ => await RunSingleOrderAsync(request, cancellationToken)
        };
    }

    /// <summary>
    /// Creates architecture-specific timeline events.
    /// </summary>
    protected abstract IReadOnlyList<ScenarioEvent> CreateTimeline(RunScenarioRequest request, OrderResponse order, InventoryResponse inventory);

    private async Task<ArchitectureRunResult> RunSingleOrderAsync(RunScenarioRequest request, CancellationToken cancellationToken)
    {
        var prepared = PrepareSingleOrderRequest(request);
        var stopwatch = Stopwatch.StartNew();

        await ResetInventoryAsync(prepared.ProductId, prepared.InitialStock, cancellationToken);
        var order = await PlaceOrderAsync(prepared, cancellationToken);
        var inventory = await GetInventoryAsync(prepared.ProductId, cancellationToken);

        stopwatch.Stop();
        return ToResult(prepared, order, inventory, stopwatch.ElapsedMilliseconds, CreateTimeline(prepared, order, inventory));
    }

    private async Task<ArchitectureRunResult> RunConcurrentOrdersAsync(RunScenarioRequest request, CancellationToken cancellationToken)
    {
        var prepared = PrepareSingleOrderRequest(request with
        {
            Quantity = Math.Max(1, request.Quantity),
            InitialStock = request.InitialStock,
            SimulatePaymentFailure = false
        });

        var stopwatch = Stopwatch.StartNew();
        await ResetInventoryAsync(prepared.ProductId, prepared.InitialStock, cancellationToken);

        var tasks = Enumerable.Range(1, prepared.ConcurrentRequests)
            .Select(index => PlaceOrderAsync(prepared with
            {
                OrderId = Guid.NewGuid(),
                IdempotencyKey = $"{prepared.IdempotencyKey}-{index}"
            }, cancellationToken))
            .ToArray();

        var orders = await Task.WhenAll(tasks);
        var inventory = await GetInventoryAsync(prepared.ProductId, cancellationToken);
        stopwatch.Stop();

        var completed = orders.Count(order => order.Status == OrderStatus.Completed);
        var rejected = orders.Count(order => order.Status == OrderStatus.Rejected);
        var representative = orders.FirstOrDefault(order => order.Status == OrderStatus.Completed) ?? orders[0];

        return new ArchitectureRunResult(
            architectureName,
            representative.Status,
            rejected > 0 ? "SomeOrdersRejected" : representative.Reason,
            completed,
            rejected,
            inventory.AvailableQuantity,
            stopwatch.ElapsedMilliseconds,
            CreateTimeline(prepared, representative, inventory));
    }

    private async Task<ArchitectureRunResult> RunDuplicateRequestAsync(RunScenarioRequest request, CancellationToken cancellationToken)
    {
        var prepared = PrepareSingleOrderRequest(request);
        var stopwatch = Stopwatch.StartNew();

        await ResetInventoryAsync(prepared.ProductId, prepared.InitialStock, cancellationToken);
        var first = await PlaceOrderAsync(prepared, cancellationToken);
        var second = await PlaceOrderAsync(prepared, cancellationToken);
        var inventory = await GetInventoryAsync(prepared.ProductId, cancellationToken);

        stopwatch.Stop();
        return new ArchitectureRunResult(
            architectureName,
            second.Status,
            second.Reason ?? "IdempotentResultReturned",
            second.Status == OrderStatus.Completed ? 1 : 0,
            second.Status == OrderStatus.Rejected ? 1 : 0,
            inventory.AvailableQuantity,
            stopwatch.ElapsedMilliseconds,
            CreateTimeline(prepared, second, inventory));
    }

    private static RunScenarioRequest PrepareSingleOrderRequest(RunScenarioRequest request)
    {
        return request.Scenario switch
        {
            ScenarioKind.InsufficientInventory => request with
            {
                InitialStock = Math.Min(request.InitialStock, Math.Max(0, request.Quantity - 1)),
                SimulatePaymentFailure = false
            },
            ScenarioKind.PaymentFailureCompensation => request with
            {
                InitialStock = Math.Max(request.InitialStock, request.Quantity),
                SimulatePaymentFailure = true
            },
            ScenarioKind.SuccessfulOrder => request with
            {
                InitialStock = Math.Max(request.InitialStock, request.Quantity),
                SimulatePaymentFailure = false
            },
            _ => request
        };
    }

    private async Task ResetInventoryAsync(string productId, int quantity, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("/api/scenarios/reset", new ResetInventoryRequest(productId, quantity), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<OrderResponse> PlaceOrderAsync(RunScenarioRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("/api/orders", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(cancellationToken))!;
    }

    private async Task<InventoryResponse> GetInventoryAsync(string productId, CancellationToken cancellationToken)
    {
        return (await httpClient.GetFromJsonAsync<InventoryResponse>($"/api/inventory/{productId}", cancellationToken))!;
    }

    private ArchitectureRunResult ToResult(
        RunScenarioRequest request,
        OrderResponse order,
        InventoryResponse inventory,
        long elapsedMilliseconds,
        IReadOnlyList<ScenarioEvent> events)
    {
        return new ArchitectureRunResult(
            architectureName,
            order.Status,
            order.Reason,
            order.Status == OrderStatus.Completed ? 1 : 0,
            order.Status == OrderStatus.Rejected ? 1 : 0,
            inventory.AvailableQuantity,
            elapsedMilliseconds,
            events);
    }
}
