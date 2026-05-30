using Ordering.Grains.Contracts;

namespace Ordering.Grains.Interfaces;

/// <summary>
/// Represents inventory for one product identity.
/// </summary>
public interface IInventoryItemGrain : IGrainWithStringKey
{
    /// <summary>
    /// Resets available inventory for deterministic scenarios.
    /// </summary>
    Task<InventorySnapshot> ResetAsync(int quantity);

    /// <summary>
    /// Gets the current inventory snapshot.
    /// </summary>
    Task<InventorySnapshot> GetAsync();

    /// <summary>
    /// Reserves inventory for an order.
    /// </summary>
    Task<InventoryReservationResult> ReserveAsync(Guid reservationId, Guid orderId, int quantity);

    /// <summary>
    /// Releases a previous reservation.
    /// </summary>
    Task<InventorySnapshot> ReleaseAsync(Guid reservationId);
}
