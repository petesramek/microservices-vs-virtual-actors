namespace Ordering.Grains.Grains;

using Ordering.Grains.Contracts;
using Ordering.Grains.Interfaces;
using Orleans;

/// <summary>
/// Grain that simulates payment authorization for one customer/account identity.
/// </summary>
public sealed class PaymentAccountGrain : Grain, IPaymentAccountGrain {
    private readonly Dictionary<string, PaymentAuthorizationResult> _authorizations = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<PaymentAuthorizationResult> AuthorizeAsync(Guid paymentId, Guid orderId, string idempotencyKey, bool simulateFailure) {
        if (_authorizations.TryGetValue(idempotencyKey, out PaymentAuthorizationResult? existing)) {
            return Task.FromResult(existing);
        }

        PaymentAuthorizationResult result = simulateFailure
            ? new PaymentAuthorizationResult(Authorized: false, $"PaymentFailed")
            : new PaymentAuthorizationResult(Authorized: true, Reason: null);

        _authorizations[idempotencyKey] = result;
        return Task.FromResult(result);
    }
}
