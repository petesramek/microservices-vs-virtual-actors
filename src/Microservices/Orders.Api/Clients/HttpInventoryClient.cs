namespace Orders.Api.Clients;

using Hosting.ServiceDefaults.Observability;
using Orders.Api.Clients.Abstraction;
using System.Net.Http.Json;
using Workbench.Contracts;

/// <summary>
/// HTTP implementation of the inventory service client.
/// </summary>
/// <param name="httpClient">The HTTP client.</param>
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

    private static HttpRequestMessage CreateScenarioRequest(
        HttpMethod method,
        string requestUri,
        object? content = null) {
        var message = new HttpRequestMessage(method, requestUri);

        message.Headers.TryAddWithoutValidation(
            ScenarioInstrumentation.Headers.ScenarioRun,
            ScenarioInstrumentation.Headers.ScenarioRunValue);

        if (content is not null) {
            message.Content = JsonContent.Create(content);
        }

        return message;
    }

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
