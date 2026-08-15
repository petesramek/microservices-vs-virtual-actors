namespace Ordering.Grains.Grains.Abstraction;

using Ordering.Grains.Contracts;
using Orleans;

/// <summary>
/// Represents payment behavior for one customer/account identity.
/// </summary>
[Alias($"Ordering.Grains.Abstraction.IPaymentAccountGrain")]
public interface IPaymentAccountGrain : IGrainWithStringKey {
    /// <summary>
    /// Authorizes a payment request.
    /// </summary>
    [Alias($"AuthorizeAsync")]
    Task<PaymentAuthorizationResult> AuthorizeAsync(Guid paymentId, Guid orderId, string idempotencyKey, bool simulateFailure);
}
