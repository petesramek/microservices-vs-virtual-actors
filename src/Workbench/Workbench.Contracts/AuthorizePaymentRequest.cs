namespace Workbench.Contracts;

/// <summary>
/// Represents a request to authorize payment for an order.
/// </summary>
/// <param name="PaymentId">The payment identifier.</param>
/// <param name="OrderId">The order identifier.</param>
/// <param name="CustomerId">The customer identifier.</param>
/// <param name="IdempotencyKey">The idempotency key.</param>
/// <param name="SimulateFailure">A value indicating whether payment failure should be simulated.</param>
public sealed record AuthorizePaymentRequest(
    Guid PaymentId,
    Guid OrderId,
    string CustomerId,
    string IdempotencyKey,
    bool SimulateFailure);
