namespace Inventory.Api.Models;

/// <summary>
/// Represents a reserved inventory quantity.
/// </summary>
public sealed class InventoryReservation
{
    /// <summary>
    /// Gets or sets the reservation identifier.
    /// </summary>
    public Guid ReservationId { get; set; }

    /// <summary>
    /// Gets or sets the order identifier.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Gets or sets the product identifier.
    /// </summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reserved quantity.
    /// </summary>
    public int Quantity { get; set; }
}
