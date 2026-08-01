namespace Comparison.Gateway.Clients;

using Comparison.Contracts;

/// <summary>
/// Runs scenarios against the microservice-style implementation.
/// </summary>
/// <param name="httpClient">The HTTP client configured for Orders API.</param>
public sealed class MicroservicesArchitectureClient(HttpClient httpClient) : HttpArchitectureClient(httpClient, "Microservices") {
    /// <inheritdoc />
    protected override IReadOnlyList<ScenarioEvent> CreateTimeline(RunScenarioRequest request, OrderResponse order, InventoryResponse inventory) {
        return request.Scenario switch {
            ScenarioKind.InsufficientInventory =>
            [
                new ScenarioEvent("Orders.Api", "Received order request."),
                new ScenarioEvent("Inventory.Api", "Rejected reservation because inventory was insufficient."),
                new ScenarioEvent("Orders.Api", "Rejected order without calling Payments.Api."),
            ],
            ScenarioKind.PaymentFailureCompensation =>
            [
                new ScenarioEvent("Orders.Api", "Received order request."),
                new ScenarioEvent("Inventory.Api", "Reserved inventory."),
                new ScenarioEvent("Payments.Api", "Rejected payment authorization."),
                new ScenarioEvent("Inventory.Api", "Released reserved inventory."),
                new ScenarioEvent("Orders.Api", "Rejected order because payment failed."),
            ],
            ScenarioKind.ConcurrentOrders =>
            [
                new ScenarioEvent("Orders.Api", "Received concurrent order requests."),
                new ScenarioEvent("Inventory.Api", "Serialized reservations through inventory update logic."),
                new ScenarioEvent("Payments.Api", "Authorized successful reservations."),
                new ScenarioEvent("Orders.Api", "Completed only orders with successful reservations."),
            ],
            ScenarioKind.DuplicateRequest =>
            [
                new ScenarioEvent("Orders.Api", "Received duplicate request."),
                new ScenarioEvent("Orders.Api", "Returned existing order result for idempotency key."),
            ],
            _ =>
            [
                new ScenarioEvent("Orders.Api", "Received order request."),
                new ScenarioEvent("Inventory.Api", "Reserved inventory."),
                new ScenarioEvent("Payments.Api", "Authorized payment."),
                new ScenarioEvent("Orders.Api", "Completed order."),
            ],
        };
    }
}
