namespace Inventory.Api.Models;

/// <summary>
/// Represents an inventory quantity reserved for an order.
/// </summary>
/// <remarks>
/// A reservation associates one order with a quantity of one product. The
/// reservation identifier uniquely identifies the allocation for idempotent
/// reservation and release operations.
/// </remarks>
public sealed class InventoryReservation {
    /// <summary>
    /// Gets or sets the reservation identifier.
    /// </summary>
    /// <value>The unique identifier of the inventory reservation.</value>
    public Guid ReservationId { get; set; }

    /// <summary>
    /// Gets or sets the order identifier.
    /// </summary>
    /// <value>The identifier of the order that owns the reservation.</value>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Gets or sets the product identifier.
    /// </summary>
    /// <value>The identifier of the reserved product.</value>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reserved quantity.
    /// </summary>
    /// <value>The quantity allocated to the associated order.</value>
    public int Quantity { get; set; }
}
