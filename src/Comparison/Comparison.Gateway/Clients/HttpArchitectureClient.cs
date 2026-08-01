namespace Comparison.Gateway.Clients;

using ArchitectureComparison.Contracts;
using System.Diagnostics;
using System.Net.Http.Json;

/// <summary>
/// Base HTTP implementation for architecture clients.
/// </summary>
/// <param name="httpClient">The HTTP client.</param>
/// <param name="architectureName">The architecture name.</param>
public abstract class HttpArchitectureClient(HttpClient httpClient, string architectureName) : IArchitectureClient {
    /// <inheritdoc />
    public async Task<ArchitectureRunResult> RunAsync(RunScenarioRequest request, CancellationToken cancellationToken) {
        return request.Scenario switch {
            ScenarioKind.ConcurrentOrders => await RunConcurrentOrdersAsync(request, cancellationToken),
            ScenarioKind.HotProductContention => await RunConcurrentOrdersAsync(request, cancellationToken),
            ScenarioKind.DuplicateRequest => await RunDuplicateRequestAsync(request, cancellationToken),
            _ => await RunSingleOrderAsync(request, cancellationToken)
        };
    }

    /// <summary>
    /// Creates architecture-specific timeline events for single-order scenarios.
    /// </summary>
    /// <param name="request">The scenario request.</param>
    /// <param name="order">The order response.</param>
    /// <param name="inventory">The final inventory response.</param>
    /// <returns>The timeline events.</returns>
    protected abstract IReadOnlyList<ScenarioEvent> CreateTimeline(
        RunScenarioRequest request,
        OrderResponse order,
        InventoryResponse inventory);

    private async Task<ArchitectureRunResult> RunSingleOrderAsync(
        RunScenarioRequest request,
        CancellationToken cancellationToken) {
        var prepared = PrepareScenarioRequest(request);
        var stopwatch = Stopwatch.StartNew();

        await ResetInventoryAsync(prepared.ProductId, prepared.InitialStock, cancellationToken);
        var order = await PlaceOrderAsync(prepared, cancellationToken);
        var inventory = await GetInventoryAsync(prepared.ProductId, cancellationToken);

        stopwatch.Stop();

        if (prepared.Scenario == ScenarioKind.PaymentTimeoutAfterReservation) {
            return ToResult(
                prepared,
                order with { Reason = "PaymentTimeout" },
                inventory,
                stopwatch.ElapsedMilliseconds,
                CreatePaymentTimeoutTimeline(prepared, inventory),
                "PaymentTimeout");
        }

        return ToResult(
            prepared,
            order,
            inventory,
            stopwatch.ElapsedMilliseconds,
            CreateTimeline(prepared, order, inventory));
    }

    private async Task<ArchitectureRunResult> RunConcurrentOrdersAsync(
        RunScenarioRequest request,
        CancellationToken cancellationToken) {
        var prepared = PrepareScenarioRequest(request with {
            Quantity = Math.Max(1, request.Quantity),
            InitialStock = request.InitialStock,
            SimulatePaymentFailure = false
        });

        var stopwatch = Stopwatch.StartNew();
        await ResetInventoryAsync(prepared.ProductId, prepared.InitialStock, cancellationToken);

        var tasks = Enumerable.Range(1, prepared.ConcurrentRequests)
            .Select(index => PlaceOrderAsync(prepared with {
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
            CreateAggregateTimeline(prepared, completed, rejected, inventory.AvailableQuantity),
            prepared.ConcurrentRequests,
            0);
    }

    private async Task<ArchitectureRunResult> RunDuplicateRequestAsync(
        RunScenarioRequest request,
        CancellationToken cancellationToken) {
        var prepared = PrepareScenarioRequest(request with {
            ConcurrentRequests = Math.Max(2, request.ConcurrentRequests),
            InitialStock = Math.Max(request.InitialStock, request.Quantity),
            SimulatePaymentFailure = false
        });

        var stopwatch = Stopwatch.StartNew();
        await ResetInventoryAsync(prepared.ProductId, prepared.InitialStock, cancellationToken);

        var tasks = Enumerable.Range(1, prepared.ConcurrentRequests)
            .Select(_ => PlaceOrderAsync(prepared, cancellationToken))
            .ToArray();

        var responses = await Task.WhenAll(tasks);
        var inventory = await GetInventoryAsync(prepared.ProductId, cancellationToken);
        stopwatch.Stop();

        var representative = responses.FirstOrDefault(response => response.Status == OrderStatus.Completed) ?? responses[0];
        var uniqueCompletedOrders = responses
            .Where(response => response.Status == OrderStatus.Completed)
            .Select(response => response.OrderId)
            .Distinct()
            .Count();
        var uniqueRejectedOrders = uniqueCompletedOrders == 0 && responses.Any(response => response.Status == OrderStatus.Rejected)
            ? 1
            : 0;
        var uniqueLogicalResults = Math.Max(1, uniqueCompletedOrders + uniqueRejectedOrders);
        var idempotentResponses = Math.Max(0, prepared.ConcurrentRequests - uniqueLogicalResults);

        return new ArchitectureRunResult(
            architectureName,
            representative.Status,
            idempotentResponses > 0 ? "IdempotentResultReturned" : representative.Reason,
            uniqueCompletedOrders,
            uniqueRejectedOrders,
            inventory.AvailableQuantity,
            stopwatch.ElapsedMilliseconds,
            CreateDuplicateTimeline(prepared, uniqueCompletedOrders, uniqueRejectedOrders, idempotentResponses, inventory.AvailableQuantity),
            prepared.ConcurrentRequests,
            idempotentResponses);
    }

    private IReadOnlyList<ScenarioEvent> CreatePaymentTimeoutTimeline(
        RunScenarioRequest request,
        InventoryResponse inventory) {
        if (architectureName.Equals("Virtual Actors", StringComparison.OrdinalIgnoreCase)) {
            return
            [
                new ScenarioEvent("Ordering.Api", "Received order request."),
                new ScenarioEvent("OrderGrain", "Started order workflow."),
                new ScenarioEvent("InventoryItemGrain", $"Reserved inventory for quantity {request.Quantity}."),
                new ScenarioEvent("PaymentAccountGrain", "Payment authorization timed out."),
                new ScenarioEvent("InventoryItemGrain", "Released inventory reservation after timeout."),
                new ScenarioEvent("OrderGrain", $"Rejected order after payment timeout. Remaining inventory is {inventory.AvailableQuantity}.")
            ];
        }

        return
        [
            new ScenarioEvent("Orders.Api", "Received order request."),
            new ScenarioEvent("Inventory.Api", $"Reserved inventory for quantity {request.Quantity}."),
            new ScenarioEvent("Payments.Api", "Payment authorization timed out."),
            new ScenarioEvent("Inventory.Api", "Released inventory reservation after timeout."),
            new ScenarioEvent("Orders.Api", $"Rejected order after payment timeout. Remaining inventory is {inventory.AvailableQuantity}.")
        ];
    }

    private IReadOnlyList<ScenarioEvent> CreateAggregateTimeline(
        RunScenarioRequest request,
        int completed,
        int rejected,
        int remainingInventory) {
        var totalSubmissions = completed + rejected;
        if (architectureName.Equals("Virtual Actors", StringComparison.OrdinalIgnoreCase)) {
            return
            [
                new ScenarioEvent("Ordering.Api", $"Received {totalSubmissions} concurrent order submissions."),
                new ScenarioEvent("InventoryItemGrain", $"Serialized reservation attempts for hot product '{request.ProductId}'."),
                new ScenarioEvent("InventoryItemGrain", $"Reserved inventory for {completed} submissions."),
                new ScenarioEvent("InventoryItemGrain", $"Rejected {rejected} submissions after stock was exhausted."),
                new ScenarioEvent("OrderGrain", $"Completed {completed} submissions and rejected {rejected} submissions. Remaining inventory is {remainingInventory}.")
            ];
        }

        return
        [
            new ScenarioEvent("Orders.Api", $"Received {totalSubmissions} concurrent order submissions."),
            new ScenarioEvent("Inventory.Api", $"Protected reservation attempts for hot product '{request.ProductId}'."),
            new ScenarioEvent("Inventory.Api", $"Reserved inventory for {completed} submissions."),
            new ScenarioEvent("Inventory.Api", $"Rejected {rejected} submissions after stock was exhausted."),
            new ScenarioEvent("Orders.Api", $"Completed {completed} submissions and rejected {rejected} submissions. Remaining inventory is {remainingInventory}.")
        ];
    }

    private IReadOnlyList<ScenarioEvent> CreateDuplicateTimeline(
        RunScenarioRequest request,
        int uniqueCompletedOrders,
        int uniqueRejectedOrders,
        int idempotentResponses,
        int remainingInventory) {
        if (architectureName.Equals("Virtual Actors", StringComparison.OrdinalIgnoreCase)) {
            return
            [
                new ScenarioEvent("Ordering.Api", $"Received {request.ConcurrentRequests} duplicate request submissions."),
                new ScenarioEvent("OrderGrain", "Serialized duplicate submissions for one order identity."),
                new ScenarioEvent("InventoryItemGrain", $"Reserved inventory once for quantity {request.Quantity}."),
                new ScenarioEvent("OrderGrain", $"Created {uniqueCompletedOrders} unique successful order and returned {idempotentResponses} idempotent duplicate responses."),
                new ScenarioEvent("Ordering.Api", $"Rejected submissions: {uniqueRejectedOrders}. Remaining inventory is {remainingInventory}.")
            ];
        }

        return
        [
            new ScenarioEvent("Orders.Api", $"Received {request.ConcurrentRequests} duplicate request submissions."),
            new ScenarioEvent("Orders.Api", "Created one unique order for the idempotency key."),
            new ScenarioEvent("Inventory.Api", $"Reserved inventory once for quantity {request.Quantity}."),
            new ScenarioEvent("Orders.Api", $"Returned {idempotentResponses} idempotent duplicate responses."),
            new ScenarioEvent("Orders.Api", $"Rejected submissions: {uniqueRejectedOrders}. Remaining inventory is {remainingInventory}.")
        ];
    }

    private static RunScenarioRequest PrepareScenarioRequest(RunScenarioRequest request) {
        var isolatedRequest = request with {
            OrderId = Guid.NewGuid(),
            IdempotencyKey = $"{request.Scenario}-{Guid.NewGuid():N}"
        };

        return isolatedRequest.Scenario switch {
            ScenarioKind.InsufficientInventory => isolatedRequest with {
                InitialStock = Math.Min(isolatedRequest.InitialStock, Math.Max(0, isolatedRequest.Quantity - 1)),
                SimulatePaymentFailure = false
            },
            ScenarioKind.PaymentFailureCompensation => isolatedRequest with {
                InitialStock = Math.Max(isolatedRequest.InitialStock, isolatedRequest.Quantity),
                SimulatePaymentFailure = true
            },
            ScenarioKind.PaymentTimeoutAfterReservation => isolatedRequest with {
                InitialStock = Math.Max(isolatedRequest.InitialStock, isolatedRequest.Quantity),
                SimulatePaymentFailure = true
            },
            ScenarioKind.SuccessfulOrder => isolatedRequest with {
                InitialStock = Math.Max(isolatedRequest.InitialStock, isolatedRequest.Quantity),
                SimulatePaymentFailure = false
            },
            _ => isolatedRequest
        };
    }

    private async Task ResetInventoryAsync(string productId, int quantity, CancellationToken cancellationToken) {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/scenarios/reset") {
            Content = JsonContent.Create(new ResetInventoryRequest(productId, quantity))
        };
        AddCorrelationHeader(message);

        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<OrderResponse> PlaceOrderAsync(RunScenarioRequest request, CancellationToken cancellationToken) {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/orders") {
            Content = JsonContent.Create(request)
        };
        AddCorrelationHeader(message);

        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(cancellationToken))!;
    }

    private async Task<InventoryResponse> GetInventoryAsync(string productId, CancellationToken cancellationToken) {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"/api/inventory/{productId}");
        AddCorrelationHeader(message);

        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InventoryResponse>(cancellationToken))!;
    }

    private static void AddCorrelationHeader(HttpRequestMessage message) {
        var correlationId = CorrelationContext.CurrentCorrelationId;
        if (!string.IsNullOrWhiteSpace(correlationId)) {
            message.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
        }
    }

    private ArchitectureRunResult ToResult(
        RunScenarioRequest request,
        OrderResponse order,
        InventoryResponse inventory,
        long elapsedMilliseconds,
        IReadOnlyList<ScenarioEvent> events,
        string? reason = null) {
        return new ArchitectureRunResult(
            architectureName,
            order.Status,
            reason ?? order.Reason,
            order.Status == OrderStatus.Completed ? 1 : 0,
            order.Status == OrderStatus.Rejected ? 1 : 0,
            inventory.AvailableQuantity,
            elapsedMilliseconds,
            events,
            1,
            0);
    }
}


