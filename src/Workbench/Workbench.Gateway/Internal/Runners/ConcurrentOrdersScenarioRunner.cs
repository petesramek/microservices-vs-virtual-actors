namespace Workbench.Gateway.Internal.Runners;

using Hosting.ServiceDefaults.Observability.Metrics;
using System.Globalization;
using Workbench.Contracts.Inventory;
using Workbench.Contracts.Orders;
using Workbench.Contracts.Scenarios;
using Workbench.Gateway.Internal.Clients.Abstraction;
using Workbench.Gateway.Internal.Runners.Abstraction;

/// <summary>
/// Executes scenarios containing distinct concurrent order submissions.
/// </summary>
internal sealed class ConcurrentOrdersScenarioRunner : ScenarioRunner {
    /// <summary>
    /// Identifies a result containing rejected submissions.
    /// </summary>
    private const string SomeOrdersRejectedReason = "SomeOrdersRejected";

    /// <summary>
    /// Stores the supported concurrent-order scenarios.
    /// </summary>
    private static readonly IReadOnlySet<ScenarioKind> Scenarios =
        new HashSet<ScenarioKind> {
            ScenarioKind.ConcurrentOrders,
            ScenarioKind.HotProductContention,
        };

    /// <summary>
    /// Records workflow execution metrics.
    /// </summary>
    private readonly ScenarioMetrics _metrics;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="ConcurrentOrdersScenarioRunner"/> class.
    /// </summary>
    /// <param name="metrics">The scenario metrics recorder.</param>
    public ConcurrentOrdersScenarioRunner(ScenarioMetrics metrics)
        : base(metrics) {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    /// <inheritdoc />
    public override IReadOnlySet<ScenarioKind> SupportedScenarios => Scenarios;


    /// <summary>
    /// Submits distinct order identities concurrently.
    /// </summary>
    protected override Task<OrderResponse[]> SubmitOrdersAsync(IServiceClient serviceClient, RunScenarioRequest request, CancellationToken cancellationToken) {
        Task<OrderResponse>[] tasks = Enumerable
            .Range(1, request.ConcurrentRequests)
            .Select(index => serviceClient.PlaceOrderAsync(request with {
                OrderId = Guid.NewGuid(),
                IdempotencyKey = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{request.IdempotencyKey}-{index}"),
            }, cancellationToken))
            .ToArray();

        return Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    protected override ScenarioExecutionResult CreateResult(IServiceClient serviceClient, RunScenarioRequest request, IReadOnlyList<OrderResponse> responses, InventoryResponse inventory, long elapsedMilliseconds) {

        int completed = responses
            .Count(static order => order.Status == OrderStatus.Completed);
        int rejected = responses
            .Count(static order => order.Status == OrderStatus.Rejected);

        OrderResponse representative = responses
            .FirstOrDefault(static order => order.Status == OrderStatus.Completed)
            ?? responses[0];

        return new ScenarioExecutionResult(
            serviceClient.Name,
            representative.Status,
            rejected > 0
                ? SomeOrdersRejectedReason
                : representative.Reason,
            completed,
            rejected,
            inventory.AvailableQuantity,
            elapsedMilliseconds,
            CreateTimeline(
                serviceClient.Name,
                request,
                completed,
                rejected,
                inventory.AvailableQuantity),
            request.ConcurrentRequests,
            0);
    }

    /// <summary>
    /// Determines whether a service name identifies the virtual actor path.
    /// </summary>
    private static bool IsVirtualActors(string serviceName) {
        return serviceName.Equals(
            "Virtual Actors",
            StringComparison.OrdinalIgnoreCase);
    }

    protected override RunScenarioRequest PrepareRequest(RunScenarioRequest request) {
        return request with {
            Quantity = Math.Max(1, request.Quantity),
            InitialStock = request.InitialStock,
            SimulatePaymentFailure = false,
        };
    }

    /// <summary>
    /// Creates the architecture-specific concurrent-order timeline.
    /// </summary>
    private static IReadOnlyList<ScenarioEvent> CreateTimeline(
        string serviceName,
        RunScenarioRequest request,
        int completed,
        int rejected,
        int remainingInventory) {
        int totalSubmissions = completed + rejected;

        if (IsVirtualActors(serviceName)) {
            return [
                new ScenarioEvent(
                    "Ordering.Api",
                    $"Received {totalSubmissions} concurrent order submissions."),
                new ScenarioEvent(
                    "InventoryItemGrain",
                    $"Serialized reservation attempts for hot product '{request.ProductId}'."),
                new ScenarioEvent(
                    "InventoryItemGrain",
                    $"Reserved inventory for {completed} submissions."),
                new ScenarioEvent(
                    "InventoryItemGrain",
                    "Rejected {rejected} submissions after stock was exhausted."),
                new ScenarioEvent(
                    "OrderGrain",
                    "Completed {completed} submissions and rejected {rejected} submissions. Remaining inventory is {remainingInventory}."),
            ];
        }

        return [
            new ScenarioEvent(
                "Orders.Api",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Received {totalSubmissions} concurrent order submissions.")),
            new ScenarioEvent(
                "Inventory.Api",
                $"Protected reservation attempts for hot product "
                    + $"'{request.ProductId}'."),
            new ScenarioEvent(
                "Inventory.Api",
                $"Reserved inventory for {completed} submissions."),
            new ScenarioEvent(
                "Inventory.Api",
                 $"Rejected {rejected} submissions after stock was exhausted."),
            new ScenarioEvent(
                "Orders.Api",
                $"Completed {completed} submissions and rejected {rejected} submissions. Remaining inventory is {remainingInventory}."),
        ];
    }
}
