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
        if (_authorizations.TryGetValue(idempotencyKey, out var existing)) {
            return Task.FromResult(existing);
        }

        var result = simulateFailure
            ? new PaymentAuthorizationResult(false, $"PaymentFailed")
            : new PaymentAuthorizationResult(true, null);

        _authorizations[idempotencyKey] = result;
        return Task.FromResult(result);
    }
}
