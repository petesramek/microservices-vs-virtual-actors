namespace Microservices.Tests.Infrastructure;

using ArchitectureComparison.Contracts;
using Orders.Api.Clients.Abstraction;

/// <summary>
/// Fake payments client used by Orders API tests.
/// </summary>
public sealed class FakePaymentsClient : IPaymentsClient {
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, AuthorizePaymentResponse> _responses = new(StringComparer.OrdinalIgnoreCase);

    public Task<AuthorizePaymentResponse> AuthorizeAsync(AuthorizePaymentRequest request, CancellationToken cancellationToken) {
        lock (_syncRoot) {
            if (_responses.TryGetValue(request.IdempotencyKey, out var existing)) {
                return Task.FromResult(existing);
            }

            var response = request.SimulateFailure
                ? new AuthorizePaymentResponse(false, "PaymentFailed")
                : new AuthorizePaymentResponse(true, null);

            _responses[request.IdempotencyKey] = response;
            return Task.FromResult(response);
        }
    }
}
