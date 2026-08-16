namespace Ordering.Grains.Grains;

using Ordering.Grains.Contracts;
using Ordering.Grains.Grains.Abstraction;
using Ordering.Grains.State;
using Orleans;
using Orleans.Runtime;

/// <summary>
/// Simulates payment authorization for one customer or payment-account
/// identity.
/// </summary>
/// <remarks>
/// Authorization outcomes are persisted by idempotency key. Repeating a request
/// with the same key returns the previously persisted result, independent of the
/// newly supplied simulation flag.
/// </remarks>
public sealed class PaymentAccountGrain : Grain, IPaymentAccountGrain {
    /// <summary>
    /// Identifies the persisted payment-account state within the grain
    /// activation.
    /// </summary>
    private const string StateName = "payment-account";

    /// <summary>
    /// Identifies the Orleans storage provider used for payment-account state.
    /// </summary>
    private const string StorageProviderName = "OrderingStorage";

    /// <summary>
    /// Provides access to the persisted payment-account state.
    /// </summary>
    private readonly IPersistentState<PaymentAccountState> _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentAccountGrain"/>
    /// class.
    /// </summary>
    /// <param name="state">The persistent payment-account state accessor.</param>
    public PaymentAccountGrain(
        [PersistentState(StateName, StorageProviderName)]
        IPersistentState<PaymentAccountState> state) {
        _state = state;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Existing idempotency keys return their stored authorization result
    /// without writing state. New results are persisted before being returned.
    /// The state dictionary defines the key-comparison semantics.
    /// </remarks>
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
