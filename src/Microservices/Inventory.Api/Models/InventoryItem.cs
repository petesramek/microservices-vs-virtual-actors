namespace Inventory.Api.Models;

/// <summary>
/// Represents available inventory for one product.
/// </summary>
public sealed class InventoryItem
{
    /// <summary>
    /// Gets or sets the product identifier.
    /// </summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets available quantity.
    /// </summary>
    public int AvailableQuantity { get; set; }
}
