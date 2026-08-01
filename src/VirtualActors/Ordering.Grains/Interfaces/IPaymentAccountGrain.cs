namespace Ordering.Grains.Interfaces;

using Ordering.Grains.Contracts;
using Orleans;

/// <summary>
/// Represents payment behavior for one customer/account identity.
/// </summary>
public interface IPaymentAccountGrain : IGrainWithStringKey {
    /// <summary>
    /// Authorizes a payment request.
    /// </summary>
    Task<PaymentAuthorizationResult> AuthorizeAsync(Guid paymentId, Guid orderId, string idempotencyKey, bool simulateFailure);
}
