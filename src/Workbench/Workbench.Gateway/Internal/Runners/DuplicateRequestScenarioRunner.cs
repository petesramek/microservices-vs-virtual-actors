namespace Workbench.Gateway.Internal.Runners;

using Hosting.ServiceDefaults.Observability.Metrics;
using Workbench.Contracts.Inventory;
using Workbench.Contracts.Orders;
using Workbench.Contracts.Scenarios;
using Workbench.Gateway.Internal.Clients.Abstraction;
using Workbench.Gateway.Internal.Runners.Abstraction;

/// <summary>
/// Executes concurrent submissions of one logical order to observe idempotent
/// response behavior.
/// </summary>
internal sealed class DuplicateRequestScenarioRunner : ScenarioRunner {
    /// <summary>
    /// Identifies an idempotently replayed result.
    /// </summary>
    private const string IdempotentResultReason =
        "IdempotentResultReturned";

    /// <summary>
    /// Stores the supported duplicate-request scenario.
    /// </summary>
    public IReadOnlySet<ScenarioKind> Scenarios =
        new HashSet<ScenarioKind> {
            ScenarioKind.DuplicateRequest,
        };

    /// <summary>
    /// Records workflow execution metrics.
    /// </summary>
    private readonly ScenarioMetrics _metrics;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="DuplicateRequestScenarioRunner"/> class.
    /// </summary>
    /// <param name="metrics">The scenario metrics recorder.</param>
    public DuplicateRequestScenarioRunner(ScenarioMetrics metrics)
        : base(metrics) {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    /// <inheritdoc />
    public override IReadOnlySet<ScenarioKind> SupportedScenarios => Scenarios;

    /// <inheritdoc />
    protected override RunScenarioRequest PrepareRequest(RunScenarioRequest request) {
        return request with {
            ConcurrentRequests = Math.Max(2, request.ConcurrentRequests),
            InitialStock = Math.Max(request.InitialStock, request.Quantity),
            SimulatePaymentFailure = false,
        };
    }


    /// <summary>
    /// Creates the normalized duplicate-request result.
    /// </summary>
    protected override Task<OrderResponse[]> SubmitOrdersAsync(IServiceClient serviceClient, RunScenarioRequest request, CancellationToken cancellationToken) {
        Task<OrderResponse>[] tasks = Enumerable
            .Range(1, request.ConcurrentRequests)
            .Select(_ => serviceClient.PlaceOrderAsync(
        request,
        cancellationToken))
            .ToArray();

        return Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    protected override ScenarioExecutionResult CreateResult(IServiceClient serviceClient, RunScenarioRequest request, IReadOnlyList<OrderResponse> responses, InventoryResponse inventory, long elapsedMilliseconds) {
        DuplicateRequestOutcome outcome = AnalyzeResponses(
            responses,
            request.ConcurrentRequests);

        return new ScenarioExecutionResult(
            serviceClient.Name,
            outcome.Representative.Status,
            outcome.IdempotentResponses > 0
                ? IdempotentResultReason
                : outcome.Representative.Reason,
            outcome.UniqueCompletedOrders,
            outcome.UniqueRejectedOrders,
            inventory.AvailableQuantity,
            elapsedMilliseconds,
            CreateTimeline(
                serviceClient.Name,
                request,
                outcome,
                inventory.AvailableQuantity),
            request.ConcurrentRequests,
            outcome.IdempotentResponses);
    }

    /// <summary>
    /// Analyzes logical outcomes represented by duplicate responses.
    /// </summary>
    private static DuplicateRequestOutcome AnalyzeResponses(
        IReadOnlyList<OrderResponse> responses,
        int totalSubmissions) {
        if (responses.Count == 0) {
            throw new ArgumentException(
                "At least one duplicate response is required.",
                nameof(responses));
        }

        OrderResponse representative = responses.FirstOrDefault(
            static response => response.Status == OrderStatus.Completed)
            ?? responses[0];
        int uniqueCompletedOrders = responses
            .Where(static response =>
                response.Status == OrderStatus.Completed)
            .Select(static response => response.OrderId)
            .Distinct()
            .Count();
        int uniqueRejectedOrders = uniqueCompletedOrders == 0
            && responses.Any(static response =>
                response.Status == OrderStatus.Rejected)
                ? 1
                : 0;
        int uniqueLogicalResults = Math.Max(
            1,
            uniqueCompletedOrders + uniqueRejectedOrders);
        int idempotentResponses = Math.Max(
            0,
            totalSubmissions - uniqueLogicalResults);

        return new DuplicateRequestOutcome(
            representative,
            uniqueCompletedOrders,
            uniqueRejectedOrders,
            idempotentResponses);
    }

    /// <summary>
    /// Creates the architecture-specific duplicate-request timeline.
    /// </summary>
    private static IReadOnlyList<ScenarioEvent> CreateTimeline(
        string serviceName,
        RunScenarioRequest request,
        DuplicateRequestOutcome outcome,
        int remainingInventory) {
        if (IsVirtualActors(serviceName)) {
            return [
                new ScenarioEvent(
                    "Ordering.Api",
                    $"Received {request.ConcurrentRequests} duplicate request submissions."),
                new ScenarioEvent(
                    "OrderGrain",
                    "Serialized duplicate submissions for one order identity."),
                new ScenarioEvent(
                    "InventoryItemGrain",
                    $"Reserved inventory once for quantity {request.Quantity}."),
                new ScenarioEvent(
                    "OrderGrain",
                    $"Created {outcome.UniqueCompletedOrders} unique successful order and returned {outcome.IdempotentResponses} idempotent duplicate responses."),
                new ScenarioEvent(
                    "Ordering.Api",
                    $"Rejected submissions: {outcome.UniqueRejectedOrders}. Remaining inventory is {remainingInventory}."),
            ];
        }

        return [
            new ScenarioEvent(
                "Orders.Api",
                $"Received {request.ConcurrentRequests} duplicate request submissions."),
            new ScenarioEvent(
                "Orders.Api",
                "Created one unique order for the idempotency key."),
            new ScenarioEvent(
                "Inventory.Api",
                $"Reserved inventory once for quantity {request.Quantity}."),
            new ScenarioEvent(
                "Orders.Api",
                $"Returned {outcome.IdempotentResponses} idempotent duplicate responses."),
            new ScenarioEvent(
                "Orders.Api",
                $"Rejected submissions: {outcome.UniqueRejectedOrders}. Remaining inventory is {remainingInventory}."),
        ];
    }

    /// <summary>
    /// Determines whether a service name identifies the virtual actor path.
    /// </summary>
    private static bool IsVirtualActors(string serviceName) {
        return serviceName.Equals(
            "Virtual Actors",
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Represents logical outcomes derived from duplicate responses.
    /// </summary>
    private readonly record struct DuplicateRequestOutcome(
        OrderResponse Representative,
        int UniqueCompletedOrders,
        int UniqueRejectedOrders,
        int IdempotentResponses);
}
