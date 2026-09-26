namespace Ordering.Grains.Grains;

using Ordering.Grains.Contracts;
using Ordering.Grains.Grains.Abstraction;
using Ordering.Grains.State;
using Orleans;
using Orleans.Runtime;

/// <summary>
/// Owns the available quantity and active reservations for one inventory item.
/// </summary>
/// <remarks>
/// The grain identity is the product identifier. Orleans serializes calls to a
/// grain activation, so inventory mutations for the same product are processed
/// one at a time. Successful mutations are persisted before a result is
/// returned.
/// </remarks>
public sealed class InventoryItemGrain : Grain, IInventoryItemGrain {
    /// <summary>
    /// Identifies the persisted inventory state within the grain activation.
    /// </summary>
    private const string StateName = "inventory";

    /// <summary>
    /// Identifies the Orleans storage provider used for inventory state.
    /// </summary>
    private const string StorageProviderName = "OrderingStorage";

    /// <summary>
    /// Provides access to the persisted inventory state.
    /// </summary>
    private readonly IPersistentState<InventoryItemState> _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="InventoryItemGrain"/> class.
    /// </summary>
    /// <param name="state">The persistent inventory state accessor.</param>
    public InventoryItemGrain(
        [PersistentState(StateName, StorageProviderName)]
        IPersistentState<InventoryItemState> state) {
        _state = state;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Replaces the available quantity, removes all active reservations, and
    /// persists the reset state before returning the resulting snapshot.
    /// </remarks>
    public async Task<InventorySnapshot> ResetAsync(int quantity) {
        _state.State.AvailableQuantity = quantity;
        _state.State.Reservations.Clear();

        await _state
            .WriteStateAsync()
            .ConfigureAwait(true);

        return CreateSnapshot();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns the current in-memory state of the active grain without writing
    /// to storage.
    /// </remarks>
    public Task<InventorySnapshot> GetAsync() {
        return Task.FromResult(CreateSnapshot());
    }

    /// <inheritdoc />
    /// <remarks>
    /// Reservation identifiers provide idempotency. Repeating an existing
    /// reservation returns success without decrementing inventory again. A new
    /// reservation is persisted before success is returned.
    /// </remarks>
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
    /// <remarks>
    /// Releasing an unknown reservation is idempotent and does not write state.
    /// A known reservation restores its quantity and persists the updated state
    /// before the snapshot is returned.
    /// </remarks>
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

    /// <summary>
    /// Creates an inventory snapshot from the grain identity and current state.
    /// </summary>
    /// <returns>The current inventory snapshot.</returns>
    private InventorySnapshot CreateSnapshot() {
        return new InventorySnapshot(
            this.GetPrimaryKeyString(),
            _state.State.AvailableQuantity);
    }
}
