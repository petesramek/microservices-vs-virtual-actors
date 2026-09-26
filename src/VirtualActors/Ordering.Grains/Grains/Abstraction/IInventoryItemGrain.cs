namespace Ordering.Grains.Grains.Abstraction;

using Ordering.Grains.Contracts;
using Orleans;

/// <summary>
/// Defines inventory operations for one product identity.
/// </summary>
/// <remarks>
/// The Orleans string grain key identifies the product whose inventory state is
/// addressed. The explicit aliases form part of the Orleans call contract and
/// should remain stable when the interface or its methods are refactored.
/// </remarks>
[Alias("Ordering.Grains.Grains.Abstraction.IInventoryItemGrain")]
public interface IInventoryItemGrain : IGrainWithStringKey {
    /// <summary>
    /// Resets the available inventory quantity for deterministic scenarios.
    /// </summary>
    /// <param name="quantity">
    /// The inventory quantity that becomes available after the reset.
    /// </param>
    /// <returns>
    /// A task whose result is the inventory snapshot after the reset.
    /// </returns>
    [Alias($"ResetAsync")]
    Task<InventorySnapshot> ResetAsync(int quantity);

    /// <summary>
    /// Gets the current inventory state for the product.
    /// </summary>
    /// <returns>
    /// A task whose result is the current inventory snapshot.
    /// </returns>
    [Alias($"GetAsync")]
    Task<InventorySnapshot> GetAsync();

    /// <summary>
    /// Attempts to reserve inventory for an order.
    /// </summary>
    /// <param name="reservationId">
    /// The stable identifier of the reservation request.
    /// </param>
    /// <param name="orderId">
    /// The identifier of the order requesting the inventory.
    /// </param>
    /// <param name="quantity">The quantity requested for reservation.</param>
    /// <returns>
    /// A task whose result describes the reservation outcome and resulting
    /// inventory state.
    /// </returns>
    [Alias($"ReserveAsync")]
    Task<InventoryReservationResult> ReserveAsync(
        Guid reservationId,
        Guid orderId,
        int quantity);

    /// <summary>
    /// Releases a previously recorded inventory reservation.
    /// </summary>
    /// <param name="reservationId">
    /// The stable identifier of the reservation to release.
    /// </param>
    /// <returns>
    /// A task whose result is the inventory snapshot after the release request.
    /// </returns>
    [Alias($"ReleaseAsync")]
    Task<InventorySnapshot> ReleaseAsync(Guid reservationId);
}
