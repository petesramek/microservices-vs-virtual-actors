namespace Comparison.Contracts;

/// <summary>
/// Defines the externally visible order statuses used by both implementations.
/// </summary>
public enum OrderStatus {
    /// <summary>
    /// The order has been created.
    /// </summary>
    Created,

    /// <summary>
    /// Inventory has been reserved for the order.
    /// </summary>
    InventoryReserved,

    /// <summary>
    /// Payment has been authorized for the order.
    /// </summary>
    PaymentAuthorized,

    /// <summary>
    /// The order has completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The order has been rejected.
    /// </summary>
    Rejected,
}
