namespace Ordering.Grains.Grains.Abstraction;

using Ordering.Grains.Contracts;
using Orleans;

/// <summary>
/// Defines payment-authorization operations for one customer or account
/// identity.
/// </summary>
/// <remarks>
/// The Orleans string grain key identifies the customer or account whose
/// payment behavior is addressed. The explicit aliases form part of the Orleans
/// call contract and should remain stable when the interface or its methods are
/// refactored.
/// </remarks>
[Alias("Ordering.Grains.Grains.Abstraction.IPaymentAccountGrain")]
public interface IPaymentAccountGrain : IGrainWithStringKey {
    /// <summary>
    /// Authorizes a payment request for an order.
    /// </summary>
    /// <param name="paymentId">
    /// The stable identifier of the payment request.
    /// </param>
    /// <param name="orderId">
    /// The identifier of the order associated with the payment.
    /// </param>
    /// <param name="idempotencyKey">
    /// The caller-provided key used to identify repeated authorization
    /// requests.
    /// </param>
    /// <param name="simulateFailure">
    /// A value indicating whether the showcase workflow should request the
    /// simulated authorization-failure path.
    /// </param>
    /// <returns>
    /// A task whose result describes the payment-authorization outcome.
    /// </returns>
    [Alias("AuthorizeAsync")]
    Task<PaymentAuthorizationResult> AuthorizeAsync(
        Guid paymentId,
        Guid orderId,
        string idempotencyKey,
        bool simulateFailure);
}
