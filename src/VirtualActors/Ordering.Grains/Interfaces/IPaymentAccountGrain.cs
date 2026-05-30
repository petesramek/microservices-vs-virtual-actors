using Ordering.Grains.Contracts;

namespace Ordering.Grains.Interfaces;

/// <summary>
/// Represents payment behavior for one customer/account identity.
/// </summary>
public interface IPaymentAccountGrain : IGrainWithStringKey
{
    /// <summary>
    /// Authorizes a payment request.
    /// </summary>
    Task<PaymentAuthorizationResult> AuthorizeAsync(Guid paymentId, Guid orderId, string idempotencyKey, bool simulateFailure);
}
