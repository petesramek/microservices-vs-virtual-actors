namespace Workbench.Ui.Observability.Health;

using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Workbench.Contracts.Observability.Health;
using Workbench.Contracts.Observability.Topology;
using Workbench.Gateway.Observability.Topology;

/// <summary>
/// Collects application health reports and builds the composite system health snapshot.
/// </summary>
internal sealed class SystemHealthService(
    HttpClient httpClient,
    TopologyDefinitionProvider topologyDefinitionProvider,
    TopologyHealthCalculator topologyHealthCalculator,
    IOptions<HealthEndpointOptions> healthEndpointOptions,
    TimeProvider timeProvider) {
    private static readonly JsonSerializerOptions SerializerOptions =
        CreateSerializerOptions();

    private readonly IReadOnlyDictionary<string, string> healthEndpoints =
        healthEndpointOptions.Value;

    /// <summary>
    /// Collects the latest health reports and builds the composite topology snapshot.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The latest system health snapshot.</returns>
    public async Task<TopologySnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default) {
        TopologyDefinition definition = topologyDefinitionProvider.Definition;
        DateTimeOffset checkedAtUtc = timeProvider.GetUtcNow();

        Task<KeyValuePair<string, CollectedHealth>>[] tasks = healthEndpoints
            .Select(endpoint => CollectAsync(
                endpoint.Key,
                endpoint.Value,
                checkedAtUtc,
                cancellationToken))
            .ToArray();

        KeyValuePair<string, CollectedHealth>[] reports =
            await Task.WhenAll(tasks).ConfigureAwait(false);

        var observations = new Dictionary<string, TopologyNodeHealth>(
            StringComparer.Ordinal);

        foreach ((string source, CollectedHealth collected) in reports) {
            observations[source] = collected.ServiceHealth;

            foreach ((string entryName, TopologyNodeHealth entryHealth) in
                collected.Entries) {
                observations[entryName] = entryHealth;
            }
        }

        return topologyHealthCalculator.Calculate(
            definition,
            observations,
            checkedAtUtc);
    }

    private async Task<KeyValuePair<string, CollectedHealth>> CollectAsync(
        string healthSource,
        string endpoint,
        DateTimeOffset checkedAtUtc,
        CancellationToken cancellationToken) {
        try {
            using HttpResponseMessage response = await httpClient
                .GetAsync(endpoint, cancellationToken)
                .ConfigureAwait(false);

            HealthReport? report = await response.Content
                .ReadFromJsonAsync<HealthReport>(
                    SerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            if (report is null) {
                return CreateUnavailableResult(
                    healthSource,
                    "The health endpoint returned an empty response.",
                    checkedAtUtc);
            }

            var entries = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new TopologyNodeHealth(
                    entry.Value.Status,
                    checkedAtUtc,
                    TimeSpan.FromMilliseconds(
                        entry.Value.DurationMilliseconds),
                    entry.Value.Description),
                StringComparer.Ordinal);

            var serviceHealth = new TopologyNodeHealth(
                report.Status,
                checkedAtUtc,
                TimeSpan.FromMilliseconds(report.DurationMilliseconds),
                response.IsSuccessStatusCode
                    ? null
                    : $"The health endpoint returned HTTP {(int)response.StatusCode}.");

            return new KeyValuePair<string, CollectedHealth>(
                healthSource,
                new CollectedHealth(serviceHealth, entries));
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested) {
            return CreateUnavailableResult(
                healthSource,
                "The health request timed out.",
                checkedAtUtc);
        }
        catch (HttpRequestException exception) {
            return CreateUnavailableResult(
                healthSource,
                exception.Message,
                checkedAtUtc);
        }
        catch (JsonException exception) {
            return CreateUnavailableResult(
                healthSource,
                exception.Message,
                checkedAtUtc);
        }
    }

    private static KeyValuePair<string, CollectedHealth>
        CreateUnavailableResult(
            string healthSource,
            string description,
            DateTimeOffset checkedAtUtc) {
        var health = new TopologyNodeHealth(
            HealthStatus.Unhealthy,
            checkedAtUtc,
            null,
            description);

        return new KeyValuePair<string, CollectedHealth>(
            healthSource,
            new CollectedHealth(
                health,
                new Dictionary<string, TopologyNodeHealth>(
                    StringComparer.Ordinal)));
    }

    private static JsonSerializerOptions CreateSerializerOptions() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }

    private sealed record CollectedHealth(
        TopologyNodeHealth ServiceHealth,
        IReadOnlyDictionary<string, TopologyNodeHealth> Entries);
}
