namespace Orders.Api.Internal.Clients;

using Hosting.ServiceDefaults.Observability;
using Orders.Api.Internal.Clients.Abstraction;
using System.Net.Http.Json;
using Workbench.Contracts.Inventory;

/// <summary>
/// Implements <see cref="IInventoryClient"/> by sending HTTP requests to the
/// Inventory API.
/// </summary>
/// <param name="httpClient">
/// The HTTP client configured with the Inventory API base address and request
/// pipeline.
/// </param>
public sealed class HttpInventoryClient(HttpClient httpClient)
    : IInventoryClient {
    /// <inheritdoc />
    public async Task<InventoryResponse> ResetAsync(
        ResetInventoryRequest request,
        CancellationToken cancellationToken) {
        using HttpRequestMessage message = CreateScenarioRequest(
            HttpMethod.Post,
            "/api/inventory/reset",
            request);

        return await SendAsync<InventoryResponse>(
            message,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<InventoryResponse> GetAsync(
        string productId,
        CancellationToken cancellationToken) {
        using HttpRequestMessage message = CreateScenarioRequest(
            HttpMethod.Get,
            $"/api/inventory/{Uri.EscapeDataString(productId)}");

        return await SendAsync<InventoryResponse>(
            message,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ReserveInventoryResponse> ReserveAsync(
        string productId,
        ReserveInventoryRequest request,
        CancellationToken cancellationToken) {
        using HttpRequestMessage message = CreateScenarioRequest(
            HttpMethod.Post,
            $"/api/inventory/{Uri.EscapeDataString(productId)}/reserve",
            request);

        return await SendAsync<ReserveInventoryResponse>(
            message,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<InventoryResponse> ReleaseAsync(
        string productId,
        ReleaseInventoryRequest request,
        CancellationToken cancellationToken) {
        using HttpRequestMessage message = CreateScenarioRequest(
            HttpMethod.Post,
            $"/api/inventory/{Uri.EscapeDataString(productId)}/release",
            request);

        return await SendAsync<InventoryResponse>(
            message,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an HTTP request and marks it as architecture-workbench scenario
    /// traffic.
    /// </summary>
    /// <param name="method">The HTTP method used by the request.</param>
    /// <param name="requestUri">The relative URI of the target endpoint.</param>
    /// <param name="content">
    /// The optional value serialized as JSON request content.
    /// </param>
    /// <returns>
    /// A request message containing the scenario header and optional JSON body.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="method"/> or <paramref name="requestUri"/> is
    /// <see langword="null"/>.
    /// </exception>
    private static HttpRequestMessage CreateScenarioRequest(
        HttpMethod method,
        string requestUri,
        object? content = null) {
        HttpRequestMessage message = new(method, requestUri);
        message.Headers.TryAddWithoutValidation(
            ScenarioInstrumentation.Headers.ScenarioRun,
            ScenarioInstrumentation.Headers.ScenarioRunValue);

        if (content is not null) {
            message.Content = JsonContent.Create(content);
        }

        return message;
    }

    /// <summary>
    /// Sends an HTTP request, verifies that it succeeded, and deserializes its
    /// JSON response body.
    /// </summary>
    /// <typeparam name="TResponse">The expected response-body type.</typeparam>
    /// <param name="message">The request message to send.</param>
    /// <param name="cancellationToken">
    /// The token that cancels request transmission or response deserialization.
    /// </param>
    /// <returns>
    /// A task whose result is the deserialized response body.
    /// </returns>
    /// <exception cref="HttpRequestException">
    /// The request fails, or the response has a non-success status code.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled while the operation is
    /// in progress.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The successful response does not contain a deserializable body.
    /// </exception>
    private async Task<TResponse> SendAsync<TResponse>(
        HttpRequestMessage message,
        CancellationToken cancellationToken) {
        using HttpResponseMessage response = await httpClient
            .SendAsync(message, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<TResponse>(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"The {typeof(TResponse).Name} response did not contain a body.");
    }
}
