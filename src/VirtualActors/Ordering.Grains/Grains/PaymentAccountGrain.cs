namespace Ordering.Grains.Grains;

using Ordering.Grains.Contracts;
using Ordering.Grains.Grains.Abstraction;
using Ordering.Grains.State;
using Orleans;
using Orleans.Runtime;

/// <summary>
/// Grain that simulates payment authorization for one customer/account identity.
/// </summary>
public sealed class PaymentAccountGrain : Grain, IPaymentAccountGrain {
    private const string StateName = "payment-account";
    private const string StorageProviderName = "OrderingStorage";

    private readonly IPersistentState<PaymentAccountState> _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentAccountGrain"/> class.
    /// </summary>
    /// <param name="state">The persistent payment account state.</param>
    public PaymentAccountGrain(
        [PersistentState(StateName, StorageProviderName)]
        IPersistentState<PaymentAccountState> state) {
        _state = state;
    }

    /// <inheritdoc />
    public async Task<PaymentAuthorizationResult> AuthorizeAsync(
        Guid paymentId,
        Guid orderId,
        string idempotencyKey,
        bool simulateFailure) {
        if (_state.State.Authorizations.TryGetValue(
            idempotencyKey,
            out PaymentAuthorizationResult? existing)) {
            return existing;
        }

        PaymentAuthorizationResult result = simulateFailure
            ? new PaymentAuthorizationResult(
                Authorized: false,
                "PaymentFailed")
            : new PaymentAuthorizationResult(
                Authorized: true,
                Reason: null);

        _state.State.Authorizations[idempotencyKey] = result;

        await _state
            .WriteStateAsync()
            .ConfigureAwait(true);

        return result;
    }
}
