using System.Net.Http.Json;
using ArchitectureComparison.Contracts;

namespace Orders.Api.Clients;

/// <summary>
/// HTTP implementation of the inventory service client.
/// </summary>
/// <param name="httpClient">The HTTP client.</param>
public sealed class HttpInventoryClient(HttpClient httpClient) : IInventoryClient
{
    public async Task<InventoryResponse> ResetAsync(ResetInventoryRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("/api/inventory/reset", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InventoryResponse>(cancellationToken))!;
    }

    public async Task<InventoryResponse> GetAsync(string productId, CancellationToken cancellationToken)
    {
        return (await httpClient.GetFromJsonAsync<InventoryResponse>($"/api/inventory/{productId}", cancellationToken))!;
    }

    public async Task<ReserveInventoryResponse> ReserveAsync(string productId, ReserveInventoryRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/inventory/{productId}/reserve", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReserveInventoryResponse>(cancellationToken))!;
    }

    public async Task<InventoryResponse> ReleaseAsync(string productId, ReleaseInventoryRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/inventory/{productId}/release", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InventoryResponse>(cancellationToken))!;
    }
}
