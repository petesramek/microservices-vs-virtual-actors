namespace Workbench.Contracts.Scenarios;

/// <summary>
/// Defines the deterministic workbench scenarios that can be executed against
/// the compared architecture implementations.
/// </summary>
public enum ScenarioKind {
    /// <summary>
    /// Inventory is available and payment authorization succeeds.
    /// </summary>
    SuccessfulOrder = 0,

    /// <summary>
    /// Available inventory is insufficient and the order is rejected before
    /// payment authorization.
    /// </summary>
    InsufficientInventory = 1,

    /// <summary>
    /// Inventory is reserved, payment authorization fails, and the reservation
    /// is released through compensation.
    /// </summary>
    PaymentFailureCompensation = 2,

    /// <summary>
    /// Payment authorization times out after inventory has been reserved.
    /// </summary>
    PaymentTimeoutAfterReservation = 3,

    /// <summary>
    /// Multiple orders compete concurrently for limited inventory.
    /// </summary>
    ConcurrentOrders = 4,

    /// <summary>
    /// The same order request is submitted more than once with the same
    /// idempotency key.
    /// </summary>
    DuplicateRequest = 5,

    /// <summary>
    /// Many requests compete concurrently for the same product inventory.
    /// </summary>
    HotProductContention = 6,
}
