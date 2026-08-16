namespace Orders.Api.Internal.Clients;

using Hosting.ServiceDefaults.Observability;
using Orders.Api.Internal.Clients.Abstraction;
using System.Net.Http.Json;
using Workbench.Contracts.Payments;

/// <summary>
/// Implements <see cref="IPaymentsClient"/> by sending HTTP requests to the
/// Payments API.
/// </summary>
/// <param name="httpClient">
/// The HTTP client configured with the Payments API base address and request
/// pipeline.
/// </param>
public sealed class HttpPaymentsClient(HttpClient httpClient)
    : IPaymentsClient {
    /// <inheritdoc />
    /// <exception cref="HttpRequestException">
    /// The request fails, or the response has a non-success status code.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The successful response does not contain a deserializable body.
    /// </exception>
    public async Task<AuthorizePaymentResponse> AuthorizeAsync(
        AuthorizePaymentRequest request,
        CancellationToken cancellationToken) {
        using HttpRequestMessage message = new(
            HttpMethod.Post,
            "/api/payments/authorize") {
            Content = JsonContent.Create(request),
        };

        message.Headers.TryAddWithoutValidation(
            ScenarioInstrumentation.Headers.ScenarioRun,
            ScenarioInstrumentation.Headers.ScenarioRunValue);

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
