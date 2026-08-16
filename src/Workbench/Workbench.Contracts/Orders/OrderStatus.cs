namespace Workbench.Contracts.Orders;

/// <summary>
/// Defines the externally visible states of an order workflow.
/// </summary>
/// <remarks>
/// This shared contract is used by both architecture implementations. Existing
/// member names and declaration order should remain stable unless all producers,
/// consumers, persisted representations, and serialized payloads are migrated
/// together.
/// </remarks>
public enum OrderStatus {
    /// <summary>
    /// The order has been accepted and its workflow has been initialized.
    /// </summary>
    Created,

    /// <summary>
    /// The requested inventory has been reserved for the order.
    /// </summary>
    InventoryReserved,

    /// <summary>
    /// Payment has been authorized for the order.
    /// </summary>
    PaymentAuthorized,

    /// <summary>
    /// The order workflow has completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The order workflow ended without successful completion.
    /// </summary>
    Rejected,
}
