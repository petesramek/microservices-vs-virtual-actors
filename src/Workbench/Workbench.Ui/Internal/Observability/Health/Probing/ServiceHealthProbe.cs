namespace Workbench.Ui.Internal.Observability.Health.Probing;

using global::Observability.Health;
using global::Observability.Topology.Definitions;
using global::Observability.Topology.Snapshots;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Workbench.Ui.Internal.Observability.Health.Configuration;
using Workbench.Ui.Internal.Observability.Health.Probing.Results;

/// <summary>
/// Collects availability and detailed health observations from configured
/// service endpoints.
/// </summary>
internal sealed class ServiceHealthProbe {
    /// <summary>
    /// Defines the JSON settings used to read shared health reports.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions =
        CreateSerializerOptions();

    /// <summary>
    /// Sends requests to configured health endpoints.
    /// </summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Stores the configured system-health endpoint lookups.
    /// </summary>
    private readonly SystemHealthOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceHealthProbe"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used for service probes.</param>
    /// <param name="options">The configured system-health endpoints.</param>
    public ServiceHealthProbe(
        HttpClient httpClient,
        IOptions<SystemHealthOptions> options) {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options.Value;
    }

    /// <summary>
    /// Probes every service node in a topology definition.
    /// </summary>
    /// <param name="nodeDefinitions">The topology node definitions.</param>
    /// <param name="checkedAtUtc">
    /// The timestamp shared by all observations in the collection.
    /// </param>
    /// <param name="cancellationToken">
    /// The token that cancels service probing.
    /// </param>
    /// <returns>
    /// A task whose result contains service observations indexed by node
    /// identifier.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled.
    /// </exception>
    public async Task<IReadOnlyDictionary<string, ServiceProbeResult>>
        ProbeServicesAsync(
            IReadOnlyList<TopologyNodeDefinition> nodeDefinitions,
            DateTimeOffset checkedAtUtc,
            CancellationToken cancellationToken) {
        Task<ServiceProbeResult>[] tasks = nodeDefinitions
            .Where(static node => node.Kind == TopologyNodeKind.Service)
            .Select(node => ProbeServiceAsync(
                node.Id,
                checkedAtUtc,
                cancellationToken))
            .ToArray();

        ServiceProbeResult[] results = await Task
            .WhenAll(tasks)
            .ConfigureAwait(false);

        return results.ToDictionary(
            static result => result.NodeId,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Probes availability and health for one service node concurrently.
    /// </summary>
    /// <param name="nodeId">The stable topology node identifier.</param>
    /// <param name="checkedAtUtc">The observation timestamp.</param>
    /// <param name="cancellationToken">The token that cancels probing.</param>
    /// <returns>The combined service probe result.</returns>
    private async Task<ServiceProbeResult> ProbeServiceAsync(
        string nodeId,
        DateTimeOffset checkedAtUtc,
        CancellationToken cancellationToken) {
        Task<AvailabilityProbeResult> availabilityTask =
            ProbeAvailabilityAsync(
                nodeId,
                checkedAtUtc,
                cancellationToken);
        Task<HealthProbeResult> healthTask = ProbeHealthAsync(
            nodeId,
            checkedAtUtc,
            cancellationToken);

        await Task.WhenAll(availabilityTask, healthTask)
            .ConfigureAwait(false);

        return new ServiceProbeResult(
            nodeId,
            await availabilityTask.ConfigureAwait(false),
            await healthTask.ConfigureAwait(false));
    }

    /// <summary>
    /// Probes the configured alive endpoint for one service node.
    /// </summary>
    /// <param name="nodeId">The stable topology node identifier.</param>
    /// <param name="checkedAtUtc">The observation timestamp.</param>
    /// <param name="cancellationToken">The token that cancels the request.</param>
    /// <returns>The observed service availability.</returns>
    private async Task<AvailabilityProbeResult> ProbeAvailabilityAsync(
        string nodeId,
        DateTimeOffset checkedAtUtc,
        CancellationToken cancellationToken) {
        if (!TryGetEndpoint(
                _options.AliveEndpoints,
                nodeId,
                out string? endpoint)) {
            return new AvailabilityProbeResult(
                ResourceAvailability.Unknown,
                checkedAtUtc,
                "The alive endpoint is not configured.");
        }

        try {
            using HttpResponseMessage response = await _httpClient
                .GetAsync(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? new AvailabilityProbeResult(
                    ResourceAvailability.Available,
                    checkedAtUtc,
                    Description: null)
                : new AvailabilityProbeResult(
                    ResourceAvailability.Unavailable,
                    checkedAtUtc,
                    FormatHttpStatus(
                        "The alive endpoint returned HTTP",
                        response.StatusCode));
        } catch (OperationCanceledException)
              when (!cancellationToken.IsCancellationRequested) {
            return new AvailabilityProbeResult(
                ResourceAvailability.Unavailable,
                checkedAtUtc,
                "The alive request timed out.");
        } catch (HttpRequestException) {
            return new AvailabilityProbeResult(
                ResourceAvailability.Unavailable,
                checkedAtUtc,
                "The alive endpoint could not be reached.");
        }
    }

    /// <summary>
    /// Probes and deserializes the configured health endpoint for one service
    /// node.
    /// </summary>
    /// <param name="nodeId">The stable topology node identifier.</param>
    /// <param name="checkedAtUtc">The observation timestamp.</param>
    /// <param name="cancellationToken">The token that cancels the request.</param>
    /// <returns>The observed service health.</returns>
    private async Task<HealthProbeResult> ProbeHealthAsync(
        string nodeId,
        DateTimeOffset checkedAtUtc,
        CancellationToken cancellationToken) {
        if (!TryGetEndpoint(
                _options.HealthEndpoints,
                nodeId,
                out string? endpoint)) {
            return HealthProbeResult.Unavailable(
                checkedAtUtc,
                "The health endpoint is not configured.");
        }

        try {
            using HttpResponseMessage response = await _httpClient
                .GetAsync(endpoint, cancellationToken)
                .ConfigureAwait(false);
            HealthReport? report = await response.Content
                .ReadFromJsonAsync<HealthReport>(
                    SerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            if (report is null) {
                return HealthProbeResult.Unavailable(
                    checkedAtUtc,
                    "The health endpoint returned an empty response.");
            }

            IReadOnlyDictionary<string, HealthEntryProbeResult> entries =
                report.Entries.ToDictionary(
                    static entry => entry.Key,
                    entry => new HealthEntryProbeResult(
                        entry.Value.Status,
                        checkedAtUtc,
                        TimeSpan.FromMilliseconds(
                            entry.Value.DurationMilliseconds),
                        entry.Value.Description),
                    StringComparer.Ordinal);

            return new HealthProbeResult(
                report.Status,
                checkedAtUtc,
                TimeSpan.FromMilliseconds(report.DurationMilliseconds),
                response.IsSuccessStatusCode ? null : FormatHttpStatus("The health endpoint returned HTTP", response.StatusCode),
                entries);
        } catch (OperationCanceledException)
              when (!cancellationToken.IsCancellationRequested) {
            return HealthProbeResult.Unavailable(
                checkedAtUtc,
                "The health request timed out.");
        } catch (HttpRequestException) {
            return HealthProbeResult.Unavailable(
                checkedAtUtc,
                "The health endpoint could not be reached.");
        } catch (JsonException) {
            return HealthProbeResult.Unavailable(
                checkedAtUtc,
                "The health response could not be parsed.");
        } catch (NotSupportedException) {
            return HealthProbeResult.Unavailable(
                checkedAtUtc,
                "The health response format is not supported.");
        }
    }

    /// <summary>
    /// Resolves a non-blank endpoint for a service node.
    /// </summary>
    /// <param name="endpoints">The endpoint lookup.</param>
    /// <param name="nodeId">The stable topology node identifier.</param>
    /// <param name="endpoint">The resolved endpoint when available.</param>
    /// <returns>
    /// <see langword="true"/> when a non-blank endpoint exists; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    [SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification = "Prioritizing design clarity, encapsulation, and abstractions over micro-optimization.")]
    private static bool TryGetEndpoint(
        IReadOnlyDictionary<string, string> endpoints,
        string nodeId,
        out string? endpoint) {
        return endpoints.TryGetValue(nodeId, out endpoint)
            && !string.IsNullOrWhiteSpace(endpoint);
    }

    /// <summary>
    /// Formats an HTTP status description using invariant formatting.
    /// </summary>
    /// <param name="prefix">The description prefix.</param>
    /// <param name="statusCode">The returned HTTP status code.</param>
    /// <returns>The complete status description.</returns>
    private static string FormatHttpStatus(
        string prefix,
        System.Net.HttpStatusCode statusCode) {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix} {(int)statusCode}.");
    }

    /// <summary>
    /// Creates JSON settings for shared health-report deserialization.
    /// </summary>
    /// <returns>The configured serializer options.</returns>
    private static JsonSerializerOptions CreateSerializerOptions() {
        JsonSerializerOptions options =
            new(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
