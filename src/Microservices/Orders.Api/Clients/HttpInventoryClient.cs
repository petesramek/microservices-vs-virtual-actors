namespace Orders.Api.Clients;

using Comparison.Contracts;
using Orders.Api.Clients.Abstraction;
using System.Net.Http.Json;

/// <summary>
/// HTTP implementation of the inventory service client.
/// </summary>
/// <param name="httpClient">The HTTP client.</param>
public sealed class HttpInventoryClient(HttpClient httpClient) : IInventoryClient {
    public async Task<InventoryResponse> ResetAsync(ResetInventoryRequest request, CancellationToken cancellationToken) {
        var response = await httpClient.PostAsJsonAsync($"/api/inventory/reset", request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InventoryResponse>(cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<InventoryResponse> GetAsync(string productId, CancellationToken cancellationToken) {
        return (await httpClient.GetFromJsonAsync<InventoryResponse>($"/api/inventory/{productId}", cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<ReserveInventoryResponse> ReserveAsync(string productId, ReserveInventoryRequest request, CancellationToken cancellationToken) {
        var response = await httpClient.PostAsJsonAsync($"/api/inventory/{productId}/reserve", request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReserveInventoryResponse>(cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<InventoryResponse> ReleaseAsync(string productId, ReleaseInventoryRequest request, CancellationToken cancellationToken) {
        var response = await httpClient.PostAsJsonAsync($"/api/inventory/{productId}/release", request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InventoryResponse>(cancellationToken).ConfigureAwait(false))!;
    }
}
