namespace Workbench.Contracts;

/// <summary>
/// Represents the result of an inventory reservation attempt.
/// </summary>
/// <param name="Reserved">A value indicating whether inventory was reserved.</param>
/// <param name="Reason">The rejection reason when inventory was not reserved.</param>
/// <param name="AvailableQuantity">The remaining available inventory quantity.</param>
public sealed record ReserveInventoryResponse(
    bool Reserved,
    string? Reason,
    int AvailableQuantity);
