namespace Comparison.Gateway.Scenarios;

using Comparison.Contracts;
using Comparison.Gateway.Clients;
using System.Diagnostics;

/// <summary>
/// Runs comparison scenarios through a service client.
/// </summary>
public sealed class ScenarioRunner {
    /// <summary>
    /// Runs the specified scenario through the supplied service client.
    /// </summary>
    /// <param name="serviceClient">The service client used to execute the scenario.</param>
    /// <param name="request">The scenario request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The architecture run result.</returns>
    public Task<ArchitectureRunResult> RunAsync(
        IServiceClient serviceClient,
        RunScenarioRequest request,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(serviceClient);
        ArgumentNullException.ThrowIfNull(request);

        return request.Scenario switch {
            ScenarioKind.ConcurrentOrders => RunConcurrentOrdersAsync(serviceClient, request, cancellationToken),
            ScenarioKind.HotProductContention => RunConcurrentOrdersAsync(serviceClient, request, cancellationToken),
            ScenarioKind.DuplicateRequest => RunDuplicateRequestAsync(serviceClient, request, cancellationToken),
            _ => RunSingleOrderAsync(serviceClient, request, cancellationToken),
        };
    }

    private static async Task<ArchitectureRunResult> RunSingleOrderAsync(
        IServiceClient serviceClient,
        RunScenarioRequest request,
        CancellationToken cancellationToken) {
        RunScenarioRequest prepared = PrepareScenarioRequest(request);
        var stopwatch = Stopwatch.StartNew();

        await serviceClient
            .ResetInventoryAsync(prepared.ProductId, prepared.InitialStock, cancellationToken)
            .ConfigureAwait(false);

        OrderResponse order = await serviceClient
            .PlaceOrderAsync(prepared, cancellationToken)
            .ConfigureAwait(false);

        InventoryResponse inventory = await serviceClient
            .GetInventoryAsync(prepared.ProductId, cancellationToken)
            .ConfigureAwait(false);

        stopwatch.Stop();

        if (prepared.Scenario == ScenarioKind.PaymentTimeoutAfterReservation) {
            return ToResult(
                serviceClient.Name,
                prepared,
                order with { Reason = "PaymentTimeout" },
                inventory,
                stopwatch.ElapsedMilliseconds,
                CreatePaymentTimeoutTimeline(serviceClient.Name, prepared, inventory),
                "PaymentTimeout");
        }

        return ToResult(
            serviceClient.Name,
            prepared,
            order,
            inventory,
            stopwatch.ElapsedMilliseconds,
            serviceClient.CreateTimeline(prepared, order, inventory));
    }

    private static async Task<ArchitectureRunResult> RunConcurrentOrdersAsync(
        IServiceClient serviceClient,
        RunScenarioRequest request,
        CancellationToken cancellationToken) {
        RunScenarioRequest prepared = PrepareScenarioRequest(request with {
            Quantity = Math.Max(1, request.Quantity),
            InitialStock = request.InitialStock,
            SimulatePaymentFailure = false,
        });
        var stopwatch = Stopwatch.StartNew();

        await serviceClient
            .ResetInventoryAsync(prepared.ProductId, prepared.InitialStock, cancellationToken)
            .ConfigureAwait(false);

        Task<OrderResponse>[] tasks = Enumerable.Range(1, prepared.ConcurrentRequests)
            .Select(index => serviceClient.PlaceOrderAsync(prepared with {
                OrderId = Guid.NewGuid(),
                IdempotencyKey = $"{prepared.IdempotencyKey}-{index}",
            }, cancellationToken))
            .ToArray();

        OrderResponse[] orders = await Task.WhenAll(tasks).ConfigureAwait(false);
        InventoryResponse inventory = await serviceClient
            .GetInventoryAsync(prepared.ProductId, cancellationToken)
            .ConfigureAwait(false);

        stopwatch.Stop();

        var completed = orders.Count(order => order.Status == OrderStatus.Completed);
        var rejected = orders.Count(order => order.Status == OrderStatus.Rejected);
        OrderResponse representative = orders.FirstOrDefault(order => order.Status == OrderStatus.Completed) ?? orders[0];

        return new ArchitectureRunResult(
            serviceClient.Name,
            representative.Status,
            rejected > 0 ? "SomeOrdersRejected" : representative.Reason,
            completed,
            rejected,
            inventory.AvailableQuantity,
            stopwatch.ElapsedMilliseconds,
            CreateAggregateTimeline(
                serviceClient.Name,
                prepared,
                completed,
                rejected,
                inventory.AvailableQuantity),
            prepared.ConcurrentRequests,
            0);
    }

    private static async Task<ArchitectureRunResult> RunDuplicateRequestAsync(
        IServiceClient serviceClient,
        RunScenarioRequest request,
        CancellationToken cancellationToken) {
        RunScenarioRequest prepared = PrepareScenarioRequest(request with {
            ConcurrentRequests = Math.Max(2, request.ConcurrentRequests),
            InitialStock = Math.Max(request.InitialStock, request.Quantity),
            SimulatePaymentFailure = false,
        });
        var stopwatch = Stopwatch.StartNew();

        await serviceClient
            .ResetInventoryAsync(prepared.ProductId, prepared.InitialStock, cancellationToken)
            .ConfigureAwait(false);

        Task<OrderResponse>[] tasks = Enumerable.Range(1, prepared.ConcurrentRequests)
            .Select(_ => serviceClient.PlaceOrderAsync(prepared, cancellationToken))
            .ToArray();

        OrderResponse[] responses = await Task.WhenAll(tasks).ConfigureAwait(false);
        InventoryResponse inventory = await serviceClient
            .GetInventoryAsync(prepared.ProductId, cancellationToken)
            .ConfigureAwait(false);

        stopwatch.Stop();

        OrderResponse representative = responses.FirstOrDefault(response => response.Status == OrderStatus.Completed)
            ?? responses[0];
        var uniqueCompletedOrders = responses
            .Where(response => response.Status == OrderStatus.Completed)
            .Select(response => response.OrderId)
            .Distinct()
            .Count();
        var uniqueRejectedOrders = uniqueCompletedOrders == 0
            && responses.Any(response => response.Status == OrderStatus.Rejected)
                ? 1
                : 0;
        var uniqueLogicalResults = Math.Max(1, uniqueCompletedOrders + uniqueRejectedOrders);
        var idempotentResponses = Math.Max(0, prepared.ConcurrentRequests - uniqueLogicalResults);

        return new ArchitectureRunResult(
            serviceClient.Name,
            representative.Status,
            idempotentResponses > 0 ? "IdempotentResultReturned" : representative.Reason,
            uniqueCompletedOrders,
            uniqueRejectedOrders,
            inventory.AvailableQuantity,
            stopwatch.ElapsedMilliseconds,
            CreateDuplicateTimeline(
                serviceClient.Name,
                prepared,
                uniqueCompletedOrders,
                uniqueRejectedOrders,
                idempotentResponses,
                inventory.AvailableQuantity),
            prepared.ConcurrentRequests,
            idempotentResponses);
    }

    private static RunScenarioRequest PrepareScenarioRequest(RunScenarioRequest request) {
        RunScenarioRequest isolatedRequest = request with {
            OrderId = Guid.NewGuid(),
            IdempotencyKey = $"{request.Scenario}-{Guid.NewGuid():N}",
        };

        return isolatedRequest.Scenario switch {
            ScenarioKind.InsufficientInventory => isolatedRequest with {
                InitialStock = Math.Min(isolatedRequest.InitialStock, Math.Max(0, isolatedRequest.Quantity - 1)),
                SimulatePaymentFailure = false,
            },
            ScenarioKind.PaymentFailureCompensation => isolatedRequest with {
                InitialStock = Math.Max(isolatedRequest.InitialStock, isolatedRequest.Quantity),
                SimulatePaymentFailure = true,
            },
            ScenarioKind.PaymentTimeoutAfterReservation => isolatedRequest with {
                InitialStock = Math.Max(isolatedRequest.InitialStock, isolatedRequest.Quantity),
                SimulatePaymentFailure = true,
            },
            ScenarioKind.SuccessfulOrder => isolatedRequest with {
                InitialStock = Math.Max(isolatedRequest.InitialStock, isolatedRequest.Quantity),
                SimulatePaymentFailure = false,
            },
            _ => isolatedRequest,
        };
    }

    private static ArchitectureRunResult ToResult(
        string serviceName,
        RunScenarioRequest request,
        OrderResponse order,
        InventoryResponse inventory,
        long elapsedMilliseconds,
        IReadOnlyList<ScenarioEvent> events,
        string? reason = null) {
        return new ArchitectureRunResult(
            serviceName,
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

    private static IReadOnlyList<ScenarioEvent> CreatePaymentTimeoutTimeline(
        string serviceName,
        RunScenarioRequest request,
        InventoryResponse inventory) {
        if (IsVirtualActors(serviceName)) {
            return [
                new ScenarioEvent("Ordering.Api", "Received order request."),
                new ScenarioEvent("OrderGrain", "Started order workflow."),
                new ScenarioEvent("InventoryItemGrain", $"Reserved inventory for quantity {request.Quantity}."),
                new ScenarioEvent("PaymentAccountGrain", "Payment authorization timed out."),
                new ScenarioEvent("InventoryItemGrain", "Released inventory reservation after timeout."),
                new ScenarioEvent("OrderGrain", $"Rejected order after payment timeout. Remaining inventory is {inventory.AvailableQuantity}."),
            ];
        }

        return [
            new ScenarioEvent("Orders.Api", "Received order request."),
            new ScenarioEvent("Inventory.Api", $"Reserved inventory for quantity {request.Quantity}."),
            new ScenarioEvent("Payments.Api", "Payment authorization timed out."),
            new ScenarioEvent("Inventory.Api", "Released inventory reservation after timeout."),
            new ScenarioEvent("Orders.Api", $"Rejected order after payment timeout. Remaining inventory is {inventory.AvailableQuantity}."),
        ];
    }

    private static IReadOnlyList<ScenarioEvent> CreateAggregateTimeline(
        string serviceName,
        RunScenarioRequest request,
        int completed,
        int rejected,
        int remainingInventory) {
        var totalSubmissions = completed + rejected;

        if (IsVirtualActors(serviceName)) {
            return [
                new ScenarioEvent("Ordering.Api", $"Received {totalSubmissions} concurrent order submissions."),
                new ScenarioEvent("InventoryItemGrain", $"Serialized reservation attempts for hot product '{request.ProductId}'."),
                new ScenarioEvent("InventoryItemGrain", $"Reserved inventory for {completed} submissions."),
                new ScenarioEvent("InventoryItemGrain", $"Rejected {rejected} submissions after stock was exhausted."),
                new ScenarioEvent("OrderGrain", $"Completed {completed} submissions and rejected {rejected} submissions. Remaining inventory is {remainingInventory}."),
            ];
        }

        return [
            new ScenarioEvent("Orders.Api", $"Received {totalSubmissions} concurrent order submissions."),
            new ScenarioEvent("Inventory.Api", $"Protected reservation attempts for hot product '{request.ProductId}'."),
            new ScenarioEvent("Inventory.Api", $"Reserved inventory for {completed} submissions."),
            new ScenarioEvent("Inventory.Api", $"Rejected {rejected} submissions after stock was exhausted."),
            new ScenarioEvent("Orders.Api", $"Completed {completed} submissions and rejected {rejected} submissions. Remaining inventory is {remainingInventory}."),
        ];
    }

    private static IReadOnlyList<ScenarioEvent> CreateDuplicateTimeline(
        string serviceName,
        RunScenarioRequest request,
        int uniqueCompletedOrders,
        int uniqueRejectedOrders,
        int idempotentResponses,
        int remainingInventory) {
        if (IsVirtualActors(serviceName)) {
            return [
                new ScenarioEvent("Ordering.Api", $"Received {request.ConcurrentRequests} duplicate request submissions."),
                new ScenarioEvent("OrderGrain", "Serialized duplicate submissions for one order identity."),
                new ScenarioEvent("InventoryItemGrain", $"Reserved inventory once for quantity {request.Quantity}."),
                new ScenarioEvent("OrderGrain", $"Created {uniqueCompletedOrders} unique successful order and returned {idempotentResponses} idempotent duplicate responses."),
                new ScenarioEvent("Ordering.Api", $"Rejected submissions: {uniqueRejectedOrders}. Remaining inventory is {remainingInventory}."),
            ];
        }

        return [
            new ScenarioEvent("Orders.Api", $"Received {request.ConcurrentRequests} duplicate request submissions."),
            new ScenarioEvent("Orders.Api", "Created one unique order for the idempotency key."),
            new ScenarioEvent("Inventory.Api", $"Reserved inventory once for quantity {request.Quantity}."),
            new ScenarioEvent("Orders.Api", $"Returned {idempotentResponses} idempotent duplicate responses."),
            new ScenarioEvent("Orders.Api", $"Rejected submissions: {uniqueRejectedOrders}. Remaining inventory is {remainingInventory}."),
        ];
    }

    private static bool IsVirtualActors(string serviceName) {
        return serviceName.Equals("Virtual Actors", StringComparison.OrdinalIgnoreCase);
    }
}
