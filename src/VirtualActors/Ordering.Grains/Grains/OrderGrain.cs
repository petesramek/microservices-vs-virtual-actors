namespace Ordering.Grains.Grains;

using Comparison.Contracts;
using Ordering.Grains.Contracts;
using Ordering.Grains.Interfaces;
using Ordering.Grains.State;
using Orleans;
using Orleans.Runtime;

/// <summary>
/// Grain that owns one order workflow.
/// </summary>
public sealed class OrderGrain : Grain, IOrderGrain {
    private const string StateName = "order";
    private const string StorageProviderName = "OrderingStorage";

    private readonly IPersistentState<OrderState> _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderGrain"/> class.
    /// </summary>
    /// <param name="state">The persistent order state.</param>
    public OrderGrain(
        [PersistentState(StateName, StorageProviderName)]
        IPersistentState<OrderState> state) {
        _state = state;
    }

    /// <inheritdoc />
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
        var reservationId = Guid.NewGuid();

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
    public Task<GrainOrderResult?> GetAsync() {
        return Task.FromResult(_state.State.Result);
    }

    private async Task<GrainOrderResult> SaveResultAsync(
        GrainOrderResult result) {
        _state.State.Result = result;

        await _state
            .WriteStateAsync()
            .ConfigureAwait(true);

        return result;
    }
}
