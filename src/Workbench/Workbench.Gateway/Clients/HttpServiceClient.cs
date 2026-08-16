namespace Workbench.Gateway.Clients;

using Hosting.ServiceDefaults.Observability;
using System.Net.Http.Json;
using Workbench.Contracts;
using Workbench.Gateway.Extensions;

/// <summary>
/// Provides shared HTTP operations for a scenario service.
/// </summary>
/// <param name="httpClient">The HTTP client configured for the service.</param>
/// <param name="name">The service name used in scenario results.</param>
public abstract class HttpServiceClient(HttpClient httpClient, string name)
    : IServiceClient {
    private const string CorrelationIdHeader = "X-Correlation-ID";

    /// <inheritdoc />
    public string Name { get; } = name;

    /// <inheritdoc />
    public async Task ResetInventoryAsync(
        string productId,
        int quantity,
        CancellationToken cancellationToken) {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/scenarios/reset") {
            Content = JsonContent.Create(
                new ResetInventoryRequest(productId, quantity)),
        };

        AddRequestHeaders(message);

        using HttpResponseMessage response = await httpClient
            .SendAsync(message, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task<OrderResponse> PlaceOrderAsync(
        RunScenarioRequest request,
        CancellationToken cancellationToken) {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/orders") {
            Content = JsonContent.Create(request),
        };

        AddRequestHeaders(message);

        using HttpResponseMessage response = await httpClient
            .SendAsync(message, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<OrderResponse>(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "The order response did not contain a body.");
    }

    /// <inheritdoc />
    public async Task<InventoryResponse> GetInventoryAsync(
        string productId,
        CancellationToken cancellationToken) {
        using var message = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/inventory/{Uri.EscapeDataString(productId)}");

        AddRequestHeaders(message);

        using HttpResponseMessage response = await httpClient
            .SendAsync(message, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<InventoryResponse>(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "The inventory response did not contain a body.");
    }

    /// <inheritdoc />
    public abstract IReadOnlyList<ScenarioEvent> CreateTimeline(
        RunScenarioRequest request,
        OrderResponse order,
        InventoryResponse inventory);

    private static void AddRequestHeaders(HttpRequestMessage message) {
        message.Headers.TryAddWithoutValidation(
            ScenarioInstrumentation.Headers.ScenarioRun,
            ScenarioInstrumentation.Headers.ScenarioRunValue);

        string? correlationId = CorrelationIdApplicationBuilderExtensions.CorrelationIdContext.CurrentId;

        if (!string.IsNullOrWhiteSpace(correlationId)) {
            message.Headers.TryAddWithoutValidation(
                CorrelationIdHeader,
                correlationId);
        }
    }
}
