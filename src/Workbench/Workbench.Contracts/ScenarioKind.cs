namespace Workbench.Contracts;

/// <summary>
/// Defines the supported workbench scenarios.
/// </summary>
public enum ScenarioKind {
    /// <summary>
    /// Inventory is available and payment succeeds.
    /// </summary>
    SuccessfulOrder,

    /// <summary>
    /// Inventory is insufficient and the order is rejected.
    /// </summary>
    InsufficientInventory,

    /// <summary>
    /// Inventory is reserved but payment fails and compensation is required.
    /// </summary>
    PaymentFailureCompensation,

    /// <summary>
    /// Payment times out after inventory has been reserved.
    /// </summary>
    PaymentTimeoutAfterReservation,

    /// <summary>
    /// Multiple orders compete for limited inventory.
    /// </summary>
    ConcurrentOrders,

    /// <summary>
    /// The same request is submitted more than once with the same idempotency key.
    /// </summary>
    DuplicateRequest,

    /// <summary>
    /// Many requests compete concurrently for the same product inventory.
    /// </summary>
    HotProductContention,
}
