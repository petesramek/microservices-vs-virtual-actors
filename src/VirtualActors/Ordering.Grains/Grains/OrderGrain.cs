namespace Ordering.Grains.Grains;

using Comparison.Contracts;
using Ordering.Grains.Contracts;
using Ordering.Grains.Interfaces;
using Orleans;

/// <summary>
/// Grain that owns one order workflow.
/// </summary>
public sealed class OrderGrain : Grain, IOrderGrain {
    private GrainOrderResult? _result;

    /// <inheritdoc />
    public async Task<GrainOrderResult> PlaceAsync(
        string idempotencyKey,
        string customerId,
        string productId,
        int quantity,
        bool simulatePaymentFailure) {
        if (_result is not null) {
            return _result;
        }

        var orderId = this.GetPrimaryKey();
        var reservationId = Guid.NewGuid();
        var inventory = GrainFactory.GetGrain<IInventoryItemGrain>(productId);

        var reservation = await inventory.ReserveAsync(reservationId, orderId, quantity);
        if (!reservation.Reserved) {
            _result = new GrainOrderResult(orderId, OrderStatus.Rejected.ToString(), reservation.Reason);
            return _result;
        }

        var payment = GrainFactory.GetGrain<IPaymentAccountGrain>(customerId);
        var authorization = await payment.AuthorizeAsync(Guid.NewGuid(), orderId, idempotencyKey, simulatePaymentFailure);

        if (!authorization.Authorized) {
            await inventory.ReleaseAsync(reservationId);
            _result = new GrainOrderResult(orderId, OrderStatus.Rejected.ToString(), authorization.Reason);
            return _result;
        }

        _result = new GrainOrderResult(orderId, OrderStatus.Completed.ToString(), null);
        return _result;
    }

    /// <inheritdoc />
    public Task<GrainOrderResult?> GetAsync() {
        return Task.FromResult(_result);
    }
}
