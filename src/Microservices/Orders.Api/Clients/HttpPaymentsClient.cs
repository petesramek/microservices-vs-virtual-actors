namespace Orders.Api.Clients;

using Comparison.Contracts;
using Orders.Api.Clients.Abstraction;
using System.Net.Http.Json;

/// <summary>
/// HTTP implementation of the payments service client.
/// </summary>
/// <param name="httpClient">The HTTP client.</param>
public sealed class HttpPaymentsClient(HttpClient httpClient) : IPaymentsClient {
    public async Task<AuthorizePaymentResponse> AuthorizeAsync(AuthorizePaymentRequest request, CancellationToken cancellationToken) {
        var response = await httpClient.PostAsJsonAsync("/api/payments/authorize", request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthorizePaymentResponse>(cancellationToken).ConfigureAwait(false))!;
    }
}
