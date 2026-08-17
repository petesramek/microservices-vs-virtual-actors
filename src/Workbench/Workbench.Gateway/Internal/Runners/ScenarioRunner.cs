namespace Workbench.Gateway.Internal.Runners;

using Hosting.ServiceDefaults.Observability.Metrics;
using System.Diagnostics;
using System.Globalization;
using Workbench.Contracts.Inventory;
using Workbench.Contracts.Orders;
using Workbench.Contracts.Scenarios;
using Workbench.Gateway.Internal.Clients.Abstraction;

/// <summary>
/// Prepares and runs deterministic workbench scenarios through an architecture
/// service client.
/// </summary>
internal sealed class ScenarioRunner {
    /// <summary>
    /// Records workflow execution metrics for completed scenario runs.
    /// </summary>
    private readonly ScenarioMetrics _metrics;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScenarioRunner"/> class.
    /// </summary>
    /// <param name="metrics">
    /// The metrics recorder used to observe workflow execution duration.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="metrics"/> is <see langword="null"/>.
    /// </exception>
    public ScenarioRunner(ScenarioMetrics metrics) {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    /// <summary>
    /// Runs the requested scenario through the supplied architecture service
    /// client.
    /// </summary>
    /// <param name="serviceClient">
    /// The architecture service client used to execute scenario operations.
    /// </param>
    /// <param name="request">The scenario request to prepare and execute.</param>
    /// <param name="cancellationToken">
    /// The token that cancels scenario execution.
    /// </param>
    /// <returns>
    /// A task whose result contains the architecture execution outcome.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="serviceClient"/> or <paramref name="request"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled while the scenario is
    /// running.
    /// </exception>
    public Task<ScenarioExecutionResult> RunAsync(
        IServiceClient serviceClient,
        RunScenarioRequest request,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(serviceClient);
        ArgumentNullException.ThrowIfNull(request);

        return request.Scenario switch {
            ScenarioKind.ConcurrentOrders => RunConcurrentOrdersAsync(
                serviceClient,
                request,
                cancellationToken),
            ScenarioKind.HotProductContention => RunConcurrentOrdersAsync(
                serviceClient,
                request,
                cancellationToken),
            ScenarioKind.DuplicateRequest => RunDuplicateRequestAsync(
                serviceClient,
                request,
                cancellationToken),
            _ => RunSingleOrderAsync(
                serviceClient,
                request,
                cancellationToken),
        };
    }

    /// <summary>
    /// Runs a scenario that submits one order request.
    /// </summary>
    /// <param name="serviceClient">
    /// The architecture service client used to execute scenario operations.
    /// </param>
    /// <param name="request">The scenario request.</param>
    /// <param name="cancellationToken">
    /// The token that cancels scenario execution.
    /// </param>
    /// <returns>
    /// A task whose result contains the single-order execution outcome.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled while the scenario is
    /// running.
    /// </exception>
    private async Task<ScenarioExecutionResult> RunSingleOrderAsync(
        IServiceClient serviceClient,
        RunScenarioRequest request,
        CancellationToken cancellationToken) {
        RunScenarioRequest prepared = PrepareScenarioRequest(request);
        Stopwatch stopwatch = Stopwatch.StartNew();

        await serviceClient
            .ResetInventoryAsync(
                prepared.ProductId,
                prepared.InitialStock,
                cancellationToken)
            .ConfigureAwait(false);

        OrderResponse order = await serviceClient
            .PlaceOrderAsync(prepared, cancellationToken)
            .ConfigureAwait(false);
        InventoryResponse inventory = await serviceClient
            .GetInventoryAsync(prepared.ProductId, cancellationToken)
            .ConfigureAwait(false);

        stopwatch.Stop();
        RecordWorkflowRunMetrics(serviceClient, request, stopwatch.Elapsed);

        if (prepared.Scenario == ScenarioKind.PaymentTimeoutAfterReservation) {
            return ToResult(
                serviceClient.Name,
                prepared,
                order with { Reason = "PaymentTimeout" },
                inventory,
                stopwatch.ElapsedMilliseconds,
                CreatePaymentTimeoutTimeline(
                    serviceClient.Name,
                    prepared,
                    inventory),
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

    /// <summary>
    /// Runs concurrent order submissions with distinct order and idempotency
    /// identifiers against one product.
    /// </summary>
    /// <param name="serviceClient">
    /// The architecture service client used to execute scenario operations.
    /// </param>
    /// <param name="request">The scenario request.</param>
    /// <param name="cancellationToken">
    /// The token that cancels scenario execution.
    /// </param>
    /// <returns>
    /// A task whose result aggregates completed and rejected order submissions.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled while the scenario is
    /// running.
    /// </exception>
    private async Task<ScenarioExecutionResult> RunConcurrentOrdersAsync(
        IServiceClient serviceClient,
        RunScenarioRequest request,
        CancellationToken cancellationToken) {
        RunScenarioRequest prepared = PrepareScenarioRequest(request with {
            Quantity = Math.Max(1, request.Quantity),
            InitialStock = request.InitialStock,
            SimulatePaymentFailure = false,
        });
        Stopwatch stopwatch = Stopwatch.StartNew();

        await serviceClient
            .ResetInventoryAsync(
                prepared.ProductId,
                prepared.InitialStock,
                cancellationToken)
            .ConfigureAwait(false);

        Task<OrderResponse>[] tasks = Enumerable
            .Range(1, prepared.ConcurrentRequests)
            .Select(index => serviceClient.PlaceOrderAsync(prepared with {
                OrderId = Guid.NewGuid(),
                IdempotencyKey = string.Create(CultureInfo.InvariantCulture, $"{prepared.IdempotencyKey}-{index}"),
            }, cancellationToken))
            .ToArray();

        OrderResponse[] orders = await Task.WhenAll(tasks).ConfigureAwait(false);
        InventoryResponse inventory = await serviceClient
            .GetInventoryAsync(prepared.ProductId, cancellationToken)
            .ConfigureAwait(false);

        stopwatch.Stop();
        RecordWorkflowRunMetrics(serviceClient, request, stopwatch.Elapsed);

        int completed = orders.Count(
            order => order.Status == OrderStatus.Completed);
        int rejected = orders.Count(
            order => order.Status == OrderStatus.Rejected);
        OrderResponse representative = orders.FirstOrDefault(
            order => order.Status == OrderStatus.Completed) ?? orders[0];

        return new ScenarioExecutionResult(
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

    /// <summary>
    /// Runs concurrent submissions of the same logical order to observe
    /// idempotent response behavior.
    /// </summary>
    /// <param name="serviceClient">
    /// The architecture service client used to execute scenario operations.
    /// </param>
    /// <param name="request">The scenario request.</param>
    /// <param name="cancellationToken">
    /// The token that cancels scenario execution.
    /// </param>
    /// <returns>
    /// A task whose result reports unique logical outcomes and idempotent
    /// responses.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled while the scenario is
    /// running.
    /// </exception>
    private async Task<ScenarioExecutionResult> RunDuplicateRequestAsync(
        IServiceClient serviceClient,
        RunScenarioRequest request,
        CancellationToken cancellationToken) {
        RunScenarioRequest prepared = PrepareScenarioRequest(request with {
            ConcurrentRequests = Math.Max(2, request.ConcurrentRequests),
            InitialStock = Math.Max(request.InitialStock, request.Quantity),
            SimulatePaymentFailure = false,
        });
        Stopwatch stopwatch = Stopwatch.StartNew();

        await serviceClient
            .ResetInventoryAsync(
                prepared.ProductId,
                prepared.InitialStock,
                cancellationToken)
            .ConfigureAwait(false);

        Task<OrderResponse>[] tasks = Enumerable
            .Range(1, prepared.ConcurrentRequests)
            .Select(_ => serviceClient.PlaceOrderAsync(
                prepared,
                cancellationToken))
            .ToArray();

        OrderResponse[] responses = await Task
            .WhenAll(tasks)
            .ConfigureAwait(false);
        InventoryResponse inventory = await serviceClient
            .GetInventoryAsync(prepared.ProductId, cancellationToken)
            .ConfigureAwait(false);

        stopwatch.Stop();
        RecordWorkflowRunMetrics(serviceClient, request, stopwatch.Elapsed);

        OrderResponse representative = responses.FirstOrDefault(
            response => response.Status == OrderStatus.Completed)
            ?? responses[0];
        int uniqueCompletedOrders = responses
            .Where(response => response.Status == OrderStatus.Completed)
            .Select(response => response.OrderId)
            .Distinct()
            .Count();
        int uniqueRejectedOrders = uniqueCompletedOrders == 0
            && responses.Any(
                response => response.Status == OrderStatus.Rejected)
                ? 1
                : 0;
        int uniqueLogicalResults = Math.Max(
            1,
            uniqueCompletedOrders + uniqueRejectedOrders);
        int idempotentResponses = Math.Max(
            0,
            prepared.ConcurrentRequests - uniqueLogicalResults);

        return new ScenarioExecutionResult(
            serviceClient.Name,
            representative.Status,
            idempotentResponses > 0
                ? "IdempotentResultReturned"
                : representative.Reason,
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

    /// <summary>
    /// Applies deterministic setup values required by the selected scenario.
    /// </summary>
    /// <param name="request">The original scenario request.</param>
    /// <returns>
    /// A copy of the request with inventory and payment-failure values prepared
    /// for the selected scenario.
    /// </returns>
    private static RunScenarioRequest PrepareScenarioRequest(
        RunScenarioRequest request) {
        return request.Scenario switch {
            ScenarioKind.InsufficientInventory => request with {
                InitialStock = Math.Min(
                    request.InitialStock,
                    Math.Max(0, request.Quantity - 1)),
                SimulatePaymentFailure = false,
            },
            ScenarioKind.PaymentFailureCompensation => request with {
                InitialStock = Math.Max(
                    request.InitialStock,
                    request.Quantity),
                SimulatePaymentFailure = true,
            },
            ScenarioKind.PaymentTimeoutAfterReservation => request with {
                InitialStock = Math.Max(
                    request.InitialStock,
                    request.Quantity),
                SimulatePaymentFailure = true,
            },
            ScenarioKind.SuccessfulOrder => request with {
                InitialStock = Math.Max(
                    request.InitialStock,
                    request.Quantity),
                SimulatePaymentFailure = false,
            },
            _ => request,
        };
    }

    /// <summary>
    /// Converts one order and inventory observation into a scenario execution
    /// result.
    /// </summary>
    /// <param name="serviceName">The architecture implementation name.</param>
    /// <param name="request">The prepared scenario request.</param>
    /// <param name="order">The observed order result.</param>
    /// <param name="inventory">The observed inventory result.</param>
    /// <param name="elapsedMilliseconds">
    /// The scenario execution duration in milliseconds.
    /// </param>
    /// <param name="events">The explanatory scenario timeline.</param>
    /// <param name="reason">
    /// An optional reason that overrides the order response reason.
    /// </param>
    /// <returns>The corresponding scenario execution result.</returns>
    private static ScenarioExecutionResult ToResult(
        string serviceName,
        OrderResponse order,
        InventoryResponse inventory,
        long elapsedMilliseconds,
        IReadOnlyList<ScenarioEvent> events,
        string? reason = null) {
        return new ScenarioExecutionResult(
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

    /// <summary>
    /// Creates the explanatory timeline for payment timeout after reservation.
    /// </summary>
    /// <param name="serviceName">The architecture implementation name.</param>
    /// <param name="request">The prepared scenario request.</param>
    /// <param name="inventory">The final inventory observation.</param>
    /// <returns>The architecture-specific timeout timeline.</returns>
    private static IReadOnlyList<ScenarioEvent> CreatePaymentTimeoutTimeline(
        string serviceName,
        RunScenarioRequest request,
        InventoryResponse inventory) {
        if (IsVirtualActors(serviceName)) {
            return [
                new ScenarioEvent("Ordering.Api", "Received order request."),
                new ScenarioEvent("OrderGrain", "Started order workflow."),
                new ScenarioEvent("InventoryItemGrain", string.Create(CultureInfo.InvariantCulture, $"Reserved inventory for quantity {request.Quantity}.")),
                new ScenarioEvent("PaymentAccountGrain", "Payment authorization timed out."),
                new ScenarioEvent("InventoryItemGrain", "Released inventory reservation after timeout."),
                new ScenarioEvent("OrderGrain", string.Create(CultureInfo.InvariantCulture, $"Rejected order after payment timeout. Remaining inventory is {inventory.AvailableQuantity}.")),
            ];
        }

        return [
            new ScenarioEvent("Orders.Api", "Received order request."),
            new ScenarioEvent("Inventory.Api", string.Create(CultureInfo.InvariantCulture, $"Reserved inventory for quantity {request.Quantity}.")),
            new ScenarioEvent("Payments.Api", "Payment authorization timed out."),
            new ScenarioEvent("Inventory.Api", "Released inventory reservation after timeout."),
            new ScenarioEvent("Orders.Api", string.Create(CultureInfo.InvariantCulture, $"Rejected order after payment timeout. Remaining inventory is {inventory.AvailableQuantity}.")),
        ];
    }

    /// <summary>
    /// Creates the explanatory timeline for concurrent order submissions.
    /// </summary>
    /// <param name="serviceName">The architecture implementation name.</param>
    /// <param name="request">The prepared scenario request.</param>
    /// <param name="completed">The number of completed orders.</param>
    /// <param name="rejected">The number of rejected orders.</param>
    /// <param name="remainingInventory">The final available quantity.</param>
    /// <returns>The architecture-specific aggregate timeline.</returns>
    private static IReadOnlyList<ScenarioEvent> CreateAggregateTimeline(
        string serviceName,
        RunScenarioRequest request,
        int completed,
        int rejected,
        int remainingInventory) {
        int totalSubmissions = completed + rejected;

        if (IsVirtualActors(serviceName)) {
            return [
                new ScenarioEvent("Ordering.Api", string.Create(CultureInfo.InvariantCulture, $"Received {totalSubmissions} concurrent order submissions.")),
                new ScenarioEvent("InventoryItemGrain", $"Serialized reservation attempts for hot product '{request.ProductId}'."),
                new ScenarioEvent("InventoryItemGrain", string.Create(CultureInfo.InvariantCulture, $"Reserved inventory for {completed} submissions.")),
                new ScenarioEvent("InventoryItemGrain", string.Create(CultureInfo.InvariantCulture, $"Rejected {rejected} submissions after stock was exhausted.")),
                new ScenarioEvent("OrderGrain", string.Create(CultureInfo.InvariantCulture, $"Completed {completed} submissions and rejected {rejected} submissions. Remaining inventory is {remainingInventory}.")),
            ];
        }

        return [
            new ScenarioEvent("Orders.Api", string.Create(CultureInfo.InvariantCulture, $"Received {totalSubmissions} concurrent order submissions.")),
            new ScenarioEvent("Inventory.Api", $"Protected reservation attempts for hot product '{request.ProductId}'."),
            new ScenarioEvent("Inventory.Api", string.Create(CultureInfo.InvariantCulture, $"Reserved inventory for {completed} submissions.")),
            new ScenarioEvent("Inventory.Api", string.Create(CultureInfo.InvariantCulture, $"Rejected {rejected} submissions after stock was exhausted.")),
            new ScenarioEvent("Orders.Api", string.Create(CultureInfo.InvariantCulture, $"Completed {completed} submissions and rejected {rejected} submissions. Remaining inventory is {remainingInventory}.")),
        ];
    }

    /// <summary>
    /// Creates the explanatory timeline for duplicate order submissions.
    /// </summary>
    /// <param name="serviceName">The architecture implementation name.</param>
    /// <param name="request">The prepared scenario request.</param>
    /// <param name="uniqueCompletedOrders">
    /// The number of unique successful logical orders.
    /// </param>
    /// <param name="uniqueRejectedOrders">
    /// The number of unique rejected logical orders.
    /// </param>
    /// <param name="idempotentResponses">
    /// The number of duplicate responses resolved idempotently.
    /// </param>
    /// <param name="remainingInventory">The final available quantity.</param>
    /// <returns>The architecture-specific duplicate-request timeline.</returns>
    private static IReadOnlyList<ScenarioEvent> CreateDuplicateTimeline(
        string serviceName,
        RunScenarioRequest request,
        int uniqueCompletedOrders,
        int uniqueRejectedOrders,
        int idempotentResponses,
        int remainingInventory) {
        if (IsVirtualActors(serviceName)) {
            return [
                new ScenarioEvent("Ordering.Api", string.Create(CultureInfo.InvariantCulture, $"Received {request.ConcurrentRequests} duplicate request submissions.")),
                new ScenarioEvent("OrderGrain", "Serialized duplicate submissions for one order identity."),
                new ScenarioEvent("InventoryItemGrain", string.Create(CultureInfo.InvariantCulture, $"Reserved inventory once for quantity {request.Quantity}.")),
                new ScenarioEvent("OrderGrain", string.Create(CultureInfo.InvariantCulture, $"Created {uniqueCompletedOrders} unique successful order and returned {idempotentResponses} idempotent duplicate responses.")),
                new ScenarioEvent("Ordering.Api", string.Create(CultureInfo.InvariantCulture, $"Rejected submissions: {uniqueRejectedOrders}. Remaining inventory is {remainingInventory}.")),
            ];
        }

        return [
            new ScenarioEvent("Orders.Api", string.Create(CultureInfo.InvariantCulture, $"Received {request.ConcurrentRequests} duplicate request submissions.")),
            new ScenarioEvent("Orders.Api", "Created one unique order for the idempotency key."),
            new ScenarioEvent("Inventory.Api", string.Create(CultureInfo.InvariantCulture, $"Reserved inventory once for quantity {request.Quantity}.")),
            new ScenarioEvent("Orders.Api", string.Create(CultureInfo.InvariantCulture, $"Returned {idempotentResponses} idempotent duplicate responses.")),
            new ScenarioEvent("Orders.Api", string.Create(CultureInfo.InvariantCulture, $"Rejected submissions: {uniqueRejectedOrders}. Remaining inventory is {remainingInventory}.")),
        ];
    }

    /// <summary>
    /// Determines whether an implementation name identifies the virtual actor
    /// architecture.
    /// </summary>
    /// <param name="serviceName">The architecture implementation name.</param>
    /// <returns>
    /// <see langword="true"/> when the name identifies the virtual actor
    /// implementation; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool IsVirtualActors(string serviceName) {
        return serviceName.Equals(
            "Virtual Actors",
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Records workflow duration for one architecture scenario execution.
    /// </summary>
    /// <param name="serviceClient">
    /// The architecture client that executed the workflow.
    /// </param>
    /// <param name="request">The original scenario request.</param>
    /// <param name="elapsed">The measured workflow duration.</param>
    private void RecordWorkflowRunMetrics(
        IServiceClient serviceClient,
        RunScenarioRequest request,
        TimeSpan elapsed) {
        _metrics.RecordWorkflowRun(
            elapsed,
            serviceClient.Name,
            request.Scenario.ToString());
    }
}
