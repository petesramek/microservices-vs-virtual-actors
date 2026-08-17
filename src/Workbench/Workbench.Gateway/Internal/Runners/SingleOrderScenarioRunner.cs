namespace Workbench.Gateway.Internal.Runners;

using Hosting.ServiceDefaults.Observability.Metrics;
using System.Globalization;
using Workbench.Contracts.Inventory;
using Workbench.Contracts.Orders;
using Workbench.Contracts.Scenarios;
using Workbench.Gateway.Internal.Clients.Abstraction;
using Workbench.Gateway.Internal.Runners.Abstraction;

/// <summary>
/// Executes scenarios that submit one logical order.
/// </summary>
internal sealed class SingleOrderScenarioRunner : ScenarioRunner {
    /// <summary>
    /// Identifies the normalized payment-timeout outcome.
    /// </summary>
    private const string PaymentTimeoutReason = "PaymentTimeout";

    /// <summary>
    /// Stores the supported single-order scenarios.
    /// </summary>
    private static readonly IReadOnlySet<ScenarioKind> Scenarios =
        new HashSet<ScenarioKind> {
            ScenarioKind.SuccessfulOrder,
            ScenarioKind.InsufficientInventory,
            ScenarioKind.PaymentFailureCompensation,
            ScenarioKind.PaymentTimeoutAfterReservation,
        };

    /// <summary>
    /// Records workflow execution metrics.
    /// </summary>
    private readonly ScenarioMetrics _metrics;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="SingleOrderScenarioRunner"/> class.
    /// </summary>
    /// <param name="metrics">The scenario metrics recorder.</param>
    public SingleOrderScenarioRunner(ScenarioMetrics metrics)
        : base(metrics) {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    /// <inheritdoc />
    public override IReadOnlySet<ScenarioKind> SupportedScenarios { get; } = Scenarios;

    /// <summary>
    /// Applies deterministic setup values for a single-order scenario.
    /// </summary>
    protected override RunScenarioRequest PrepareRequest(RunScenarioRequest request) {
        return request.Scenario switch {
            ScenarioKind.InsufficientInventory => request with {
                InitialStock = Math.Min(
                    request.InitialStock,
                    Math.Max(0, request.Quantity - 1)),
                SimulatePaymentFailure = false,
            },
            ScenarioKind.PaymentFailureCompensation
                or ScenarioKind.PaymentTimeoutAfterReservation => request with {
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

    protected override async Task<OrderResponse[]> SubmitOrdersAsync(IServiceClient serviceClient, RunScenarioRequest request, CancellationToken cancellationToken) {
        OrderResponse order = await serviceClient
            .PlaceOrderAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return [order];
    }

    /// <inheridoc />
    protected override ScenarioExecutionResult CreateResult(IServiceClient serviceClient, RunScenarioRequest request, IReadOnlyList<OrderResponse> responses, InventoryResponse inventory, long elapsedMilliseconds) {
        var order = responses[0];
        var isPaymentFailure = request.Scenario == ScenarioKind.PaymentTimeoutAfterReservation;

        return new ScenarioExecutionResult(
            serviceClient.Name,
            order.Status,
            isPaymentFailure ? PaymentTimeoutReason : order.Reason,
            order.Status == OrderStatus.Completed ? 1 : 0,
            order.Status == OrderStatus.Rejected ? 1 : 0,
            inventory.AvailableQuantity,
            elapsedMilliseconds,
            isPaymentFailure ?
                CreatePaymentTimeoutTimeline(
                    serviceClient.Name,
                    request,
                    inventory)
                : serviceClient.CreateTimeline(request, order, inventory),
            TotalRequestSubmissions: 1,
            IdempotentResponses: 0);
    }

    /// <summary>
    /// Creates the explanatory payment-timeout timeline.
    /// </summary>
    private static IReadOnlyList<ScenarioEvent> CreatePaymentTimeoutTimeline(
        string serviceName,
        RunScenarioRequest request,
        InventoryResponse inventory) {
        if (IsVirtualActors(serviceName)) {
            return [
                new ScenarioEvent("Ordering.Api", "Received order request."),
                new ScenarioEvent("OrderGrain", "Started order workflow."),
                new ScenarioEvent(
                    "InventoryItemGrain",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Reserved inventory for quantity {request.Quantity}.")),
                new ScenarioEvent(
                    "PaymentAccountGrain",
                    "Payment authorization timed out."),
                new ScenarioEvent(
                    "InventoryItemGrain",
                    "Released inventory reservation after timeout."),
                new ScenarioEvent(
                    "OrderGrain",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Rejected order after payment timeout. Remaining "
                            + $"inventory is {inventory.AvailableQuantity}.")),
            ];
        }

        return [
            new ScenarioEvent("Orders.Api", "Received order request."),
            new ScenarioEvent(
                "Inventory.Api",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Reserved inventory for quantity {request.Quantity}.")),
            new ScenarioEvent(
                "Payments.Api",
                "Payment authorization timed out."),
            new ScenarioEvent(
                "Inventory.Api",
                "Released inventory reservation after timeout."),
            new ScenarioEvent(
                "Orders.Api",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Rejected order after payment timeout. Remaining "
                        + $"inventory is {inventory.AvailableQuantity}.")),
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
}
