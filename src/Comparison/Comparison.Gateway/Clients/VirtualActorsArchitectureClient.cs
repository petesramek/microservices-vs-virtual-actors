namespace Comparison.Gateway.Clients;

using Comparison.Contracts;

/// <summary>
/// Runs scenarios against the virtual actor-style implementation.
/// </summary>
/// <param name="httpClient">The HTTP client configured for Ordering API.</param>
public sealed class VirtualActorsArchitectureClient(HttpClient httpClient) : HttpArchitectureClient(httpClient, $"Virtual Actors") {
    /// <inheritdoc />
    protected override IReadOnlyList<ScenarioEvent> CreateTimeline(RunScenarioRequest request, OrderResponse order, InventoryResponse inventory) {
        return request.Scenario switch {
            ScenarioKind.InsufficientInventory =>
            [
                new ScenarioEvent($"Ordering.Api", $"Received order request."),
                new ScenarioEvent($"OrderGrain", $"Started order workflow."),
                new ScenarioEvent($"InventoryItemGrain", $"Rejected reservation because inventory was insufficient."),
                new ScenarioEvent($"OrderGrain", $"Rejected order without calling PaymentAccountGrain."),
            ],
            ScenarioKind.PaymentFailureCompensation =>
            [
                new ScenarioEvent($"Ordering.Api", $"Received order request."),
                new ScenarioEvent($"OrderGrain", $"Started order workflow."),
                new ScenarioEvent($"InventoryItemGrain", $"Reserved inventory."),
                new ScenarioEvent($"PaymentAccountGrain", $"Rejected payment authorization."),
                new ScenarioEvent($"InventoryItemGrain", $"Released reserved inventory."),
                new ScenarioEvent($"OrderGrain", $"Rejected order because payment failed."),
            ],
            ScenarioKind.ConcurrentOrders =>
            [
                new ScenarioEvent($"Ordering.Api", $"Received concurrent order requests."),
                new ScenarioEvent($"OrderGrain", $"Coordinated each order workflow."),
                new ScenarioEvent($"InventoryItemGrain", $"Serialized reservations for the product identity."),
                new ScenarioEvent($"PaymentAccountGrain", $"Authorized successful reservations."),
            ],
            ScenarioKind.DuplicateRequest =>
            [
                new ScenarioEvent($"Ordering.Api", $"Received duplicate request."),
                new ScenarioEvent($"OrderGrain", $"Returned existing order result for the order identity."),
            ],
            _ =>
            [
                new ScenarioEvent($"Ordering.Api", $"Received order request."),
                new ScenarioEvent($"OrderGrain", $"Started order workflow."),
                new ScenarioEvent($"InventoryItemGrain", $"Reserved inventory."),
                new ScenarioEvent($"PaymentAccountGrain", $"Authorized payment."),
                new ScenarioEvent($"OrderGrain", $"Completed order."),
            ],
        };
    }
}
