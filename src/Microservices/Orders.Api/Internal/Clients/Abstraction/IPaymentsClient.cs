namespace Orders.Api.Internal.Clients.Abstraction;

using Workbench.Contracts.Payments;

/// <summary>
/// Defines operations for communicating with the Payments API.
/// </summary>
public interface IPaymentsClient {
    /// <summary>
    /// Requests authorization for a payment.
    /// </summary>
    /// <param name="request">
    /// The request containing the payment, order, customer, idempotency, and
    /// failure-simulation values.
    /// </param>
    /// <param name="cancellationToken">
    /// The token that cancels the payment authorization request.
    /// </param>
    /// <returns>
    /// A task whose result describes whether the payment was authorized and
    /// includes an optional failure reason.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled while the request is in
    /// progress.
    /// </exception>
    Task<AuthorizePaymentResponse> AuthorizeAsync(
        AuthorizePaymentRequest request,
        CancellationToken cancellationToken);
}
