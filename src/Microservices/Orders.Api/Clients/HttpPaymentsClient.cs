namespace Orders.Api.Clients;

using Comparison.Contracts;
using Hosting.ServiceDefaults.Telemetry;
using Orders.Api.Clients.Abstraction;
using System.Net.Http.Json;

/// <summary>
/// HTTP implementation of the payments service client.
/// </summary>
/// <param name="httpClient">The HTTP client.</param>
public sealed class HttpPaymentsClient(HttpClient httpClient)
    : IPaymentsClient {
    /// <inheritdoc />
    public async Task<AuthorizePaymentResponse> AuthorizeAsync(
        AuthorizePaymentRequest request,
        CancellationToken cancellationToken) {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/payments/authorize") {
            Content = JsonContent.Create(request),
        };

        message.Headers.TryAddWithoutValidation(
            ScenarioTelemetry.ScenarioHeaderName,
            ScenarioTelemetry.ScenarioHeaderValue);

        using HttpResponseMessage response = await httpClient
            .SendAsync(message, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<AuthorizePaymentResponse>(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "The payment authorization response did not contain a body.");
    }
}
