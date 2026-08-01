namespace Ordering.Grains.Interfaces;

using Ordering.Grains.Contracts;
using Orleans;

/// <summary>
/// Represents inventory for one product identity.
/// </summary>
[Alias($"Ordering.Grains.Interfaces.IInventoryItemGrain")]
public interface IInventoryItemGrain : IGrainWithStringKey {
    /// <summary>
    /// Resets available inventory for deterministic scenarios.
    /// </summary>
    [Alias($"ResetAsync")]
    Task<InventorySnapshot> ResetAsync(int quantity);

    /// <summary>
    /// Gets the current inventory snapshot.
    /// </summary>
    [Alias($"GetAsync")]
    Task<InventorySnapshot> GetAsync();

    /// <summary>
    /// Reserves inventory for an order.
    /// </summary>
    [Alias($"ReserveAsync")]
    Task<InventoryReservationResult> ReserveAsync(Guid reservationId, Guid orderId, int quantity);

    /// <summary>
    /// Releases a previous reservation.
    /// </summary>
    [Alias($"ReleaseAsync")]
    Task<InventorySnapshot> ReleaseAsync(Guid reservationId);
}
