namespace Orders.Api.Models;

/// <summary>
/// Represents an order persisted by the orders service.
/// </summary>
/// <remarks>
/// The record stores the order request, inventory reservation identifier, and
/// current workflow outcome. The idempotency key associates repeated placement
/// requests with the same persisted order.
/// </remarks>
public sealed class OrderRecord {
    /// <summary>
    /// Gets or sets the order identifier.
    /// </summary>
    /// <value>The unique identifier of the order.</value>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Gets or sets the idempotency key supplied for order placement.
    /// </summary>
    /// <value>The key used to associate repeated requests with this order.</value>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the customer identifier.
    /// </summary>
    /// <value>The identifier of the customer that placed the order.</value>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the product identifier.
    /// </summary>
    /// <value>The identifier of the ordered product.</value>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requested order quantity.
    /// </summary>
    /// <value>The number of product units requested by the order.</value>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the inventory reservation identifier.
    /// </summary>
    /// <value>
    /// The identifier used to reserve or release inventory for this order.
    /// </value>
    public Guid ReservationId { get; set; }

    /// <summary>
    /// Gets or sets the current order status.
    /// </summary>
    /// <value>The workflow status represented by its contract value.</value>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the order rejection reason.
    /// </summary>
    /// <value>
    /// Details explaining why the order was rejected, or
    /// <see langword="null"/> when no rejection reason applies.
    /// </value>
    public string? Reason { get; set; }
}
