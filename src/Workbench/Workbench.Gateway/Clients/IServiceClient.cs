namespace Workbench.Gateway.Clients;

using Workbench.Contracts.Inventory;
using Workbench.Contracts.Orders;
using Workbench.Contracts.Scenarios;

/// <summary>
/// Defines the service operations required to run workbench scenarios.
/// </summary>
public interface IServiceClient {
    /// <summary>
    /// Gets the service name used in scenario results.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Resets the inventory for the specified product.
    /// </summary>
    /// <param name="productId">The product identifier.</param>
    /// <param name="quantity">The inventory quantity.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    Task ResetInventoryAsync(
        string productId,
        int quantity,
        CancellationToken cancellationToken);

    /// <summary>
    /// Places an order.
    /// </summary>
    /// <param name="request">The order scenario request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The order result.</returns>
    Task<OrderResponse> PlaceOrderAsync(
        RunScenarioRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the inventory for the specified product.
    /// </summary>
    /// <param name="productId">The product identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The current inventory.</returns>
    Task<InventoryResponse> GetInventoryAsync(
        string productId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates service-specific timeline events for a completed scenario.
    /// </summary>
    /// <param name="request">The scenario request.</param>
    /// <param name="order">The order result.</param>
    /// <param name="inventory">The resulting inventory.</param>
    /// <returns>The scenario timeline events.</returns>
    IReadOnlyList<ScenarioEvent> CreateTimeline(
        RunScenarioRequest request,
        OrderResponse order,
        InventoryResponse inventory);
}
