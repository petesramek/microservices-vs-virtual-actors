namespace Workbench.Ui.Observability.Health;

using global::Observability.Health;
using global::Observability.Topology.Definitions;
using global::Observability.Topology.Evaluators.Abstraction;
using global::Observability.Topology.Snapshots;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Workbench.Ui.Observability.Topology;

/// <summary>
/// Collects service availability and detailed health observations and builds
/// a graph-oriented system health snapshot.
/// </summary>
internal sealed class SystemHealthService(
    HttpClient httpClient,
    TopologyDefinitionProvider topologyDefinitionProvider,
    IDependencyHealthEvaluator dependencyHealthEvaluator,
    IGroupHealthEvaluator groupHealthEvaluator,
    IOptions<HealthEndpointOptions> healthEndpointOptions,
    IConfiguration configuration,
    TimeProvider timeProvider) {
    private const string AliveEndpointsSectionName =
        "Observability:AliveEndpoints";

    private static readonly JsonSerializerOptions SerializerOptions =
        CreateSerializerOptions();

    private readonly HealthEndpointOptions healthEndpoints =
        healthEndpointOptions.Value;

    private readonly IReadOnlyDictionary<string, string> aliveEndpoints =
        ReadEndpoints(configuration, AliveEndpointsSectionName);

    /// <summary>
    /// Collects the latest service availability and detailed health reports
    /// and creates a graph snapshot containing direct node, edge, and group
    /// observations.
    /// </summary>
    /// <param name="cancellationToken">
    /// The token used to cancel the operation.
    /// </param>
    /// <returns>The latest graph-oriented system health snapshot.</returns>
    public async Task<TopologySnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default) {
        TopologyDefinition definition = topologyDefinitionProvider.Definition;
        DateTimeOffset generatedAtUtc = timeProvider.GetUtcNow();

        TopologyNodeDefinition[] serviceNodes = definition.Nodes
            .Where(node => node.Kind == TopologyNodeKind.Service)
            .ToArray();

        Task<ServiceObservation>[] collectionTasks = serviceNodes
            .Select(node => CollectServiceAsync(
                node.Id,
                generatedAtUtc,
                cancellationToken))
            .ToArray();

        ServiceObservation[] collectedServices = await Task
            .WhenAll(collectionTasks)
            .ConfigureAwait(false);

        IReadOnlyDictionary<string, ServiceObservation> services =
            collectedServices.ToDictionary(
                observation => observation.NodeId,
                StringComparer.Ordinal);

        TopologyNodeSnapshot[] nodes = definition.Nodes
            .Select(node => CreateNodeSnapshot(
                node,
                services,
                generatedAtUtc))
            .ToArray();

        IReadOnlyDictionary<string, TopologyNodeDefinition>
            nodeDefinitionsById = definition.Nodes.ToDictionary(
                node => node.Id,
                StringComparer.Ordinal);

        IReadOnlyDictionary<string, TopologyNodeSnapshot> nodeSnapshotsById =
            nodes.ToDictionary(
                node => node.Id,
                StringComparer.Ordinal);

        TopologyEdgeSnapshot[] edges = definition.Edges
            .Select(edge => CreateEdgeSnapshot(
                edge,
                services,
                nodeDefinitionsById,
                nodeSnapshotsById,
                generatedAtUtc))
            .ToArray();

        ILookup<string, TopologyEdgeDefinition> edgeDefinitionsBySource =
            definition.Edges.ToLookup(
                edge => edge.SourceNodeId,
                StringComparer.Ordinal);

        ILookup<string, TopologyEdgeSnapshot> edgeSnapshotsBySource =
            edges.ToLookup(
                edge => edge.SourceNodeId,
                StringComparer.Ordinal);

        TopologyNodeSnapshot[] aggregateNodes = nodes
            .Select(node => CreateAggregateNodeSnapshot(
                node,
                edgeDefinitionsBySource[node.Id].ToArray(),
                edgeSnapshotsBySource[node.Id].ToArray()))
            .ToArray();

        TopologyGroupSnapshot[] groups = definition.Groups
            .Select(group => new TopologyGroupSnapshot(
                group.Id,
                groupHealthEvaluator.Evaluate(
                    group,
                    aggregateNodes)))
            .ToArray();

        return new TopologySnapshot(
            generatedAtUtc,
            aggregateNodes,
            edges,
            groups);
    }

    private async Task<ServiceObservation> CollectServiceAsync(
        string nodeId,
        DateTimeOffset checkedAtUtc,
        CancellationToken cancellationToken) {
        Task<AvailabilityObservation> availabilityTask =
            CollectAvailabilityAsync(
                nodeId,
                checkedAtUtc,
                cancellationToken);

        Task<HealthObservation> healthTask = CollectHealthAsync(
            nodeId,
            checkedAtUtc,
            cancellationToken);

        await Task.WhenAll(availabilityTask, healthTask)
            .ConfigureAwait(false);

        return new ServiceObservation(
            nodeId,
            await availabilityTask.ConfigureAwait(false),
            await healthTask.ConfigureAwait(false));
    }

    private async Task<AvailabilityObservation> CollectAvailabilityAsync(
        string nodeId,
        DateTimeOffset checkedAtUtc,
        CancellationToken cancellationToken) {
        if (!aliveEndpoints.TryGetValue(nodeId, out string? endpoint) ||
            string.IsNullOrWhiteSpace(endpoint)) {
            return new AvailabilityObservation(
                ResourceAvailability.Unknown,
                checkedAtUtc,
                "The alive endpoint is not configured.");
        }

        try {
            using HttpResponseMessage response = await httpClient
                .GetAsync(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? new AvailabilityObservation(
                    ResourceAvailability.Available,
                    checkedAtUtc,
                    null)
                : new AvailabilityObservation(
                    ResourceAvailability.Unavailable,
                    checkedAtUtc,
                    $"The alive endpoint returned HTTP " +
                    $"{(int)response.StatusCode}.");
        } catch (OperationCanceledException)
              when (!cancellationToken.IsCancellationRequested) {
            return new AvailabilityObservation(
                ResourceAvailability.Unavailable,
                checkedAtUtc,
                "The alive request timed out.");
        } catch (HttpRequestException) {
            return new AvailabilityObservation(
                ResourceAvailability.Unavailable,
                checkedAtUtc,
                "The alive endpoint could not be reached.");
        }
    }

    private async Task<HealthObservation> CollectHealthAsync(
        string nodeId,
        DateTimeOffset checkedAtUtc,
        CancellationToken cancellationToken) {
        if (!healthEndpoints.TryGetValue(nodeId, out string? endpoint) ||
            string.IsNullOrWhiteSpace(endpoint)) {
            return HealthObservation.Unavailable(
                checkedAtUtc,
                "The health endpoint is not configured.");
        }

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
                return HealthObservation.Unavailable(
                    checkedAtUtc,
                    "The health endpoint returned an empty response.");
            }

            IReadOnlyDictionary<string, EntryObservation> entries =
                report.Entries.ToDictionary(
                    entry => entry.Key,
                    entry => new EntryObservation(
                        entry.Value.Status,
                        checkedAtUtc,
                        TimeSpan.FromMilliseconds(
                            entry.Value.DurationMilliseconds),
                        entry.Value.Description),
                    StringComparer.Ordinal);

            return new HealthObservation(
                report.Status,
                checkedAtUtc,
                TimeSpan.FromMilliseconds(report.DurationMilliseconds),
                response.IsSuccessStatusCode
                    ? null
                    : $"The health endpoint returned HTTP " +
                      $"{(int)response.StatusCode}.",
                entries);
        } catch (OperationCanceledException)
              when (!cancellationToken.IsCancellationRequested) {
            return HealthObservation.Unavailable(
                checkedAtUtc,
                "The health request timed out.");
        } catch (HttpRequestException) {
            return HealthObservation.Unavailable(
                checkedAtUtc,
                "The health endpoint could not be reached.");
        } catch (JsonException) {
            return HealthObservation.Unavailable(
                checkedAtUtc,
                "The health response could not be parsed.");
        } catch (NotSupportedException) {
            return HealthObservation.Unavailable(
                checkedAtUtc,
                "The health response format is not supported.");
        }
    }

    private static TopologyNodeSnapshot CreateNodeSnapshot(
        TopologyNodeDefinition node,
        IReadOnlyDictionary<string, ServiceObservation> services,
        DateTimeOffset checkedAtUtc) {
        ResourceAvailability? availability = node.Kind ==
            TopologyNodeKind.Service
                ? GetAvailability(node.Id, services)
                : null;

        if (node.HealthSource is null ||
            !services.TryGetValue(
                node.HealthSource.ProviderNodeId,
                out ServiceObservation? provider)) {
            return new TopologyNodeSnapshot(
                node.Id,
                availability,
                HealthStatus.Unknown,
                checkedAtUtc,
                null,
                "The node health source is unavailable.");
        }

        if (!provider.Health.Entries.TryGetValue(
                node.HealthSource.EntryKey,
                out EntryObservation? entry)) {
            return new TopologyNodeSnapshot(
                node.Id,
                availability,
                HealthStatus.Unknown,
                provider.Health.CheckedAtUtc,
                null,
                $"Health entry '{node.HealthSource.EntryKey}' was not " +
                "reported.");
        }

        return new TopologyNodeSnapshot(
            node.Id,
            availability,
            entry.Status,
            entry.CheckedAtUtc,
            entry.Duration,
            entry.Description);
    }

    private static TopologyEdgeSnapshot CreateEdgeSnapshot(
        TopologyEdgeDefinition edge,
        IReadOnlyDictionary<string, ServiceObservation> services,
        IReadOnlyDictionary<string, TopologyNodeDefinition>
            nodeDefinitionsById,
        IReadOnlyDictionary<string, TopologyNodeSnapshot>
            nodeSnapshotsById,
        DateTimeOffset checkedAtUtc) {
        if (!string.IsNullOrWhiteSpace(edge.HealthEntryKey)) {
            return CreateReportedEdgeSnapshot(
                edge,
                services,
                checkedAtUtc);
        }

        if (!nodeDefinitionsById.TryGetValue(
                edge.TargetNodeId,
                out TopologyNodeDefinition? targetDefinition) ||
            targetDefinition.Kind != TopologyNodeKind.Service ||
            !nodeSnapshotsById.TryGetValue(
                edge.TargetNodeId,
                out TopologyNodeSnapshot? targetSnapshot)) {
            return new TopologyEdgeSnapshot(
                edge.SourceNodeId,
                edge.TargetNodeId,
                HealthStatus.Unknown,
                checkedAtUtc,
                null,
                "No current target service observation is available.");
        }

        HealthStatus health = targetSnapshot.Availability switch {
            ResourceAvailability.Unavailable => HealthStatus.Unhealthy,
            ResourceAvailability.Unknown => HealthStatus.Unknown,
            ResourceAvailability.Available => targetSnapshot.Health,
            _ => HealthStatus.Unknown,
        };

        string? description = targetSnapshot.Availability ==
            ResourceAvailability.Unavailable
                ? "The target service is unavailable."
                : targetSnapshot.Description;

        return new TopologyEdgeSnapshot(
            edge.SourceNodeId,
            edge.TargetNodeId,
            health,
            targetSnapshot.CheckedAtUtc,
            targetSnapshot.Duration,
            description);
    }

    private static TopologyEdgeSnapshot CreateReportedEdgeSnapshot(
        TopologyEdgeDefinition edge,
        IReadOnlyDictionary<string, ServiceObservation> services,
        DateTimeOffset checkedAtUtc) {
        if (!services.TryGetValue(
                edge.SourceNodeId,
                out ServiceObservation? source)) {
            return new TopologyEdgeSnapshot(
                edge.SourceNodeId,
                edge.TargetNodeId,
                HealthStatus.Unknown,
                checkedAtUtc,
                null,
                "The dependency source health report is unavailable.");
        }

        if (!source.Health.Entries.TryGetValue(
                edge.HealthEntryKey!,
                out EntryObservation? entry)) {
            return new TopologyEdgeSnapshot(
                edge.SourceNodeId,
                edge.TargetNodeId,
                HealthStatus.Unknown,
                source.Health.CheckedAtUtc,
                null,
                $"Health entry '{edge.HealthEntryKey}' was not reported.");
        }

        return new TopologyEdgeSnapshot(
            edge.SourceNodeId,
            edge.TargetNodeId,
            entry.Status,
            entry.CheckedAtUtc,
            entry.Duration,
            entry.Description);
    }

    private TopologyNodeSnapshot CreateAggregateNodeSnapshot(
        TopologyNodeSnapshot node,
        IReadOnlyCollection<TopologyEdgeDefinition> edgeDefinitions,
        IReadOnlyCollection<TopologyEdgeSnapshot> edgeSnapshots) {
        if (edgeDefinitions.Count == 0) {
            return node;
        }

        HealthStatus dependencyHealth = dependencyHealthEvaluator.Evaluate(
            edgeDefinitions,
            edgeSnapshots);

        return new TopologyNodeSnapshot(
            node.Id,
            node.Availability,
            CombineHealth(
                node.Health,
                dependencyHealth),
            node.CheckedAtUtc,
            node.Duration,
            node.Description);
    }

    private static HealthStatus CombineHealth(
        HealthStatus directHealth,
        HealthStatus dependencyHealth) {
        if (directHealth == HealthStatus.Starting ||
            dependencyHealth == HealthStatus.Starting) {
            return HealthStatus.Starting;
        }

        if (directHealth == HealthStatus.Unhealthy) {
            return HealthStatus.Unhealthy;
        }

        if (dependencyHealth == HealthStatus.Unhealthy) {
            return directHealth == HealthStatus.Healthy
                ? HealthStatus.Degraded
                : HealthStatus.Unhealthy;
        }

        if (directHealth == HealthStatus.Degraded ||
            dependencyHealth == HealthStatus.Degraded) {
            return HealthStatus.Degraded;
        }

        if (directHealth == HealthStatus.Healthy) {
            return dependencyHealth == HealthStatus.Unknown
                ? HealthStatus.Degraded
                : HealthStatus.Healthy;
        }

        return HealthStatus.Unknown;
    }

    private static ResourceAvailability GetAvailability(
        string nodeId,
        IReadOnlyDictionary<string, ServiceObservation> services) {
        return services.TryGetValue(
            nodeId,
            out ServiceObservation? service)
                ? service.Availability.Availability
                : ResourceAvailability.Unknown;
    }

    private static IReadOnlyDictionary<string, string> ReadEndpoints(
        IConfiguration configuration,
        string sectionName) {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        return configuration
            .GetSection(sectionName)
            .GetChildren()
            .Where(section => !string.IsNullOrWhiteSpace(section.Value))
            .ToDictionary(
                section => section.Key,
                section => section.Value!,
                StringComparer.Ordinal);
    }

    private static JsonSerializerOptions CreateSerializerOptions() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record ServiceObservation(
        string NodeId,
        AvailabilityObservation Availability,
        HealthObservation Health);

    private sealed record AvailabilityObservation(
        ResourceAvailability Availability,
        DateTimeOffset CheckedAtUtc,
        string? Description);

    private sealed record HealthObservation(
        HealthStatus Status,
        DateTimeOffset CheckedAtUtc,
        TimeSpan? Duration,
        string? Description,
        IReadOnlyDictionary<string, EntryObservation> Entries) {
        public static HealthObservation Unavailable(
            DateTimeOffset checkedAtUtc,
            string description) {
            return new HealthObservation(
                HealthStatus.Unknown,
                checkedAtUtc,
                null,
                description,
                new Dictionary<string, EntryObservation>(
                    StringComparer.Ordinal));
        }
    }

    private sealed record EntryObservation(
        HealthStatus Status,
        DateTimeOffset CheckedAtUtc,
        TimeSpan? Duration,
        string? Description);
}
