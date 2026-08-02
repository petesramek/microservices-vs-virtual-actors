namespace Ordering.Grains.Grains;

using Ordering.Grains.Contracts;
using Ordering.Grains.Interfaces;
using Ordering.Grains.State;
using Orleans;
using Orleans.Runtime;

/// <summary>
/// Grain that owns inventory state for one product.
/// </summary>
public sealed class InventoryItemGrain : Grain, IInventoryItemGrain {
    private const string StateName = "inventory";
    private const string StorageProviderName = "OrderingStorage";

    private readonly IPersistentState<InventoryItemState> _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="InventoryItemGrain"/> class.
    /// </summary>
    /// <param name="state">The persistent inventory state.</param>
    public InventoryItemGrain(
        [PersistentState(StateName, StorageProviderName)]
        IPersistentState<InventoryItemState> state) {
        _state = state;
    }

    /// <inheritdoc />
    public async Task<InventorySnapshot> ResetAsync(int quantity) {
        _state.State.AvailableQuantity = quantity;
        _state.State.Reservations.Clear();

        await _state
            .WriteStateAsync()
            .ConfigureAwait(true);

        return CreateSnapshot();
    }

    /// <inheritdoc />
    public Task<InventorySnapshot> GetAsync() {
        return Task.FromResult(CreateSnapshot());
    }

    /// <inheritdoc />
    public async Task<InventoryReservationResult> ReserveAsync(
        Guid reservationId,
        Guid orderId,
        int quantity) {
        if (_state.State.Reservations.ContainsKey(reservationId)) {
            return new InventoryReservationResult(
                Reserved: true,
                Reason: null,
                _state.State.AvailableQuantity);
        }

        if (_state.State.AvailableQuantity < quantity) {
            return new InventoryReservationResult(
                Reserved: false,
                "InsufficientInventory",
                _state.State.AvailableQuantity);
        }

        _state.State.AvailableQuantity -= quantity;
        _state.State.Reservations[reservationId] = quantity;

        await _state
            .WriteStateAsync()
            .ConfigureAwait(true);

        return new InventoryReservationResult(
            Reserved: true,
            Reason: null,
            _state.State.AvailableQuantity);
    }

    /// <inheritdoc />
    public async Task<InventorySnapshot> ReleaseAsync(Guid reservationId) {
        if (_state.State.Reservations.Remove(
            reservationId,
            out int quantity)) {
            _state.State.AvailableQuantity += quantity;

            await _state
                .WriteStateAsync()
                .ConfigureAwait(true);
        }

        return CreateSnapshot();
    }

    private InventorySnapshot CreateSnapshot() {
        return new InventorySnapshot(
            this.GetPrimaryKeyString(),
            _state.State.AvailableQuantity);
    }
}
