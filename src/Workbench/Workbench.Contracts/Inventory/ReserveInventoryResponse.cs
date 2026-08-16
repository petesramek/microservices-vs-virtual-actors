namespace Workbench.Contracts.Inventory;

/// <summary>
/// Represents the outcome of an inventory reservation attempt.
/// </summary>
/// <param name="Reserved">
/// <see langword="true"/> when the requested inventory was reserved; otherwise,
/// <see langword="false"/>.
/// </param>
/// <param name="Reason">
/// The reason the reservation was rejected, or <see langword="null"/> when the
/// reservation succeeded.
/// </param>
/// <param name="AvailableQuantity">
/// The quantity available for new reservations after the reservation attempt.
/// </param>
public sealed record ReserveInventoryResponse(
    bool Reserved,
    string? Reason,
    int AvailableQuantity);
