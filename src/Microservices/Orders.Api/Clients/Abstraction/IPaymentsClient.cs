namespace Orders.Api.Clients.Abstraction;

using Workbench.Contracts;

/// <summary>
/// Payments service client abstraction.
/// </summary>
public interface IPaymentsClient {
    /// <summary>
    /// Authorizes a payment.
    /// </summary>
    Task<AuthorizePaymentResponse> AuthorizeAsync(AuthorizePaymentRequest request, CancellationToken cancellationToken);
}
