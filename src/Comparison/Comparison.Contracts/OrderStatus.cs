namespace Comparison.Contracts;

/// <summary>
/// Defines externally visible order states used by both implementations.
/// </summary>
public enum OrderStatus {
    /// <summary>
    /// The order has been created.
    /// </summary>
    Created,

    /// <summary>
    /// Inventory has been reserved.
    /// </summary>
    InventoryReserved,

    /// <summary>
    /// Payment has been authorized.
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
