namespace Inventory.Api.Models;

/// <summary>
/// Represents the inventory available for one product.
/// </summary>
/// <remarks>
/// The product identifier uniquely identifies the inventory item. The available
/// quantity represents stock that has not been allocated to an active
/// reservation.
/// </remarks>
public sealed class InventoryItem {
    /// <summary>
    /// Gets or sets the product identifier.
    /// </summary>
    /// <value>The unique identifier of the associated product.</value>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quantity available for new reservations.
    /// </summary>
    /// <value>The current unreserved inventory quantity.</value>
    public int AvailableQuantity { get; set; }
}
