namespace Comparison.Ui.Services;

using Comparison.Contracts;
using System.Net.Http.Json;

/// <summary>
/// Client used by the Blazor Server UI to run comparison scenarios through the gateway.
/// </summary>
/// <param name="httpClient">The HTTP client.</param>
public sealed class ScenarioRunnerClient(HttpClient httpClient) {
    /// <summary>
    /// Gets backend status from the gateway.
    /// </summary>
    /// <returns>The backend status response.</returns>
    public async Task<ServiceStatusResponse?> GetStatusAsync() {
        return await httpClient.GetFromJsonAsync<ServiceStatusResponse>($"/api/status").ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a scenario for the selected architecture.
    /// </summary>
    /// <param name="architecture">The architecture header value.</param>
    /// <param name="request">The scenario request.</param>
    /// <returns>The scenario response.</returns>
    public async Task<RunScenarioResponse?> RunAsync(string architecture, RunScenarioRequest request) {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/scenarios/run") {
            Content = JsonContent.Create(request),
        };

        httpRequest.Headers.Add($"X-Architecture", architecture);

        HttpResponseMessage response = await httpClient.SendAsync(httpRequest).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RunScenarioResponse>().ConfigureAwait(false);
    }
}
