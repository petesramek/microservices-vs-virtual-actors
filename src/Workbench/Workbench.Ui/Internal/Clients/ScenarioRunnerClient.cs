namespace Workbench.Ui.Internal.Clients;

using System.Net.Http.Json;
using Workbench.Contracts.Scenarios;

/// <summary>
/// Client used by the Blazor Server UI to run workbench scenarios through the gateway.
/// </summary>
/// <param name="httpClient">The HTTP client.</param>
internal sealed class ScenarioRunnerClient(HttpClient httpClient) {
    /// <summary>
    /// Runs a scenario.
    /// </summary>
    /// <param name="request">The scenario request.</param>
    /// <returns>The scenario response.</returns>
    public async Task<RunScenarioResponse?> RunAsync(RunScenarioRequest request) {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/scenarios/run") {
            Content = JsonContent.Create(request),
        };

        HttpResponseMessage response = await httpClient.SendAsync(httpRequest).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RunScenarioResponse>().ConfigureAwait(false);
    }
}
