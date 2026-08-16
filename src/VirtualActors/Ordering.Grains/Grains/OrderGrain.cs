namespace Ordering.Grains.Grains;

using Ordering.Grains.Contracts;
using Ordering.Grains.Grains.Abstraction;
using Ordering.Grains.State;
using Orleans;
using Orleans.Runtime;
using Workbench.Contracts.Orders;

/// <summary>
/// Owns and coordinates one order workflow.
/// </summary>
/// <remarks>
/// The grain identity is the order identifier. A persisted terminal result
/// makes repeated placement calls idempotent for that order. The workflow
/// reserves inventory, requests payment authorization, compensates the
/// reservation when payment is rejected, and then persists the terminal result.
/// </remarks>
public sealed class OrderGrain : Grain, IOrderGrain {
    /// <summary>
    /// Identifies the persisted order state within the grain activation.
    /// </summary>
    private const string StateName = "order";

    /// <summary>
    /// Identifies the Orleans storage provider used for order state.
    /// </summary>
    private const string StorageProviderName = "OrderingStorage";

    /// <summary>
    /// Provides access to the persisted order state.
    /// </summary>
    private readonly IPersistentState<OrderState> _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderGrain"/> class.
    /// </summary>
    /// <param name="state">The persistent order state accessor.</param>
    public OrderGrain(
        [PersistentState(StateName, StorageProviderName)]
        IPersistentState<OrderState> state) {
        _state = state;
    }

    /// <inheritdoc />
    /// <remarks>
    /// If a terminal result has already been persisted, that result is returned
    /// without repeating external grain calls. An inventory rejection produces
    /// a rejected order. A payment rejection releases the inventory reservation
    /// before the rejected order result is persisted.
    /// </remarks>
    public async Task<GrainOrderResult> PlaceAsync(
        string idempotencyKey,
        string customerId,
        string productId,
        int quantity,
        bool simulatePaymentFailure) {
        if (_state.State.Result is not null) {
            return _state.State.Result;
        }

        Guid orderId = this.GetPrimaryKey();
        Guid reservationId = Guid.NewGuid();
        IInventoryItemGrain inventory =
            GrainFactory.GetGrain<IInventoryItemGrain>(productId);

        InventoryReservationResult reservation = await inventory
            .ReserveAsync(reservationId, orderId, quantity)
            .ConfigureAwait(true);

        if (!reservation.Reserved) {
            return await SaveResultAsync(
                new GrainOrderResult(
                    orderId,
                    OrderStatus.Rejected.ToString(),
                    reservation.Reason)).ConfigureAwait(true);
        }

        IPaymentAccountGrain payment =
            GrainFactory.GetGrain<IPaymentAccountGrain>(customerId);

        PaymentAuthorizationResult authorization = await payment
            .AuthorizeAsync(
                Guid.NewGuid(),
                orderId,
                idempotencyKey,
                simulatePaymentFailure)
            .ConfigureAwait(true);

        if (!authorization.Authorized) {
            await inventory
                .ReleaseAsync(reservationId)
                .ConfigureAwait(true);

            return await SaveResultAsync(
                new GrainOrderResult(
                    orderId,
                    OrderStatus.Rejected.ToString(),
                    authorization.Reason)).ConfigureAwait(true);
        }

        return await SaveResultAsync(
            new GrainOrderResult(
                orderId,
                OrderStatus.Completed.ToString(),
                Reason: null)).ConfigureAwait(true);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns the persisted terminal result currently held by the active grain,
    /// or <see langword="null"/> when the workflow has not completed.
    /// </remarks>
    public Task<GrainOrderResult?> GetAsync() {
        return Task.FromResult(_state.State.Result);
    }

    /// <summary>
    /// Stores and persists a terminal order result.
    /// </summary>
    /// <param name="result">The terminal result to persist.</param>
    /// <returns>The persisted terminal result.</returns>
    private async Task<GrainOrderResult> SaveResultAsync(
        GrainOrderResult result) {
        _state.State.Result = result;

        await _state
            .WriteStateAsync()
            .ConfigureAwait(true);

        return result;
    }
}
