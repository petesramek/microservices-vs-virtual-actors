namespace Ordering.Grains.Grains;

using Ordering.Grains.Contracts;
using Ordering.Grains.Interfaces;
using Orleans;

/// <summary>
/// Grain that owns inventory state for one product.
/// </summary>
public sealed class InventoryItemGrain : Grain, IInventoryItemGrain {
    private readonly Dictionary<Guid, int> _reservations = new();
    private int _availableQuantity;

    /// <inheritdoc />
    public Task<InventorySnapshot> ResetAsync(int quantity) {
        _availableQuantity = quantity;
        _reservations.Clear();
        return Task.FromResult(CreateSnapshot());
    }

    /// <inheritdoc />
    public Task<InventorySnapshot> GetAsync() {
        return Task.FromResult(CreateSnapshot());
    }

    /// <inheritdoc />
    public Task<InventoryReservationResult> ReserveAsync(Guid reservationId, Guid orderId, int quantity) {
        if (_reservations.ContainsKey(reservationId)) {
            return Task.FromResult(new InventoryReservationResult(true, null, _availableQuantity));
        }

        if (_availableQuantity < quantity) {
            return Task.FromResult(new InventoryReservationResult(false, "InsufficientInventory", _availableQuantity));
        }

        _availableQuantity -= quantity;
        _reservations[reservationId] = quantity;

        return Task.FromResult(new InventoryReservationResult(true, null, _availableQuantity));
    }

    /// <inheritdoc />
    public Task<InventorySnapshot> ReleaseAsync(Guid reservationId) {
        if (_reservations.Remove(reservationId, out var quantity)) {
            _availableQuantity += quantity;
        }

        return Task.FromResult(CreateSnapshot());
    }

    private InventorySnapshot CreateSnapshot() {
        return new InventorySnapshot(this.GetPrimaryKeyString(), _availableQuantity);
    }
}
