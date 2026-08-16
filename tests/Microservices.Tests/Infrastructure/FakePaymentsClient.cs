namespace Microservices.Tests.Infrastructure;

using Orders.Api.Internal.Clients.Abstraction;
using Workbench.Contracts.Payments;

/// <summary>
/// Fake payments client used by Orders API tests.
/// </summary>
public sealed class FakePaymentsClient : IPaymentsClient {
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, AuthorizePaymentResponse> _responses = new(StringComparer.OrdinalIgnoreCase);

    public Task<AuthorizePaymentResponse> AuthorizeAsync(AuthorizePaymentRequest request, CancellationToken cancellationToken) {
        lock (_syncRoot) {
            if (_responses.TryGetValue(request.IdempotencyKey, out AuthorizePaymentResponse? existing)) {
                return Task.FromResult(existing);
            }

            AuthorizePaymentResponse response = request.SimulateFailure
                ? new AuthorizePaymentResponse(Authorized: false, $"PaymentFailed")
                : new AuthorizePaymentResponse(Authorized: true, Reason: null);

            _responses[request.IdempotencyKey] = response;
            return Task.FromResult(response);
        }
    }
}
