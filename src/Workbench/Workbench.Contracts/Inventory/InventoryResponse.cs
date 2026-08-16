namespace Workbench.Contracts.Inventory;

/// <summary>
/// Represents the current inventory for a product.
/// </summary>
/// <param name="ProductId">The product identifier.</param>
/// <param name="AvailableQuantity">The available inventory quantity.</param>
public sealed record InventoryResponse(
    string ProductId,
    int AvailableQuantity);
