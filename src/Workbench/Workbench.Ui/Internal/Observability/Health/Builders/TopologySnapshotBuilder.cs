namespace Workbench.Ui.Internal.Observability.Health.Builders;

using global::Observability.Health;
using global::Observability.Topology.Definitions;
using global::Observability.Topology.Evaluators.Abstraction;
using global::Observability.Topology.Snapshots;
using System.Diagnostics.CodeAnalysis;
using Workbench.Ui.Internal.Observability.Health.Probing.Results;

/// <summary>
/// Builds evaluated topology snapshots from topology definitions and collected
/// service observations.
/// </summary>
internal sealed class TopologySnapshotBuilder(
    IDependencyHealthEvaluator dependencyHealthEvaluator,
    IGroupHealthEvaluator groupHealthEvaluator) {
    /// <summary>
    /// Builds a complete topology snapshot.
    /// </summary>
    /// <param name="definition">The static topology definition.</param>
    /// <param name="services">
    /// The collected service observations indexed by node identifier.
    /// </param>
    /// <param name="generatedAtUtc">The snapshot generation timestamp.</param>
    /// <returns>The evaluated topology snapshot.</returns>
    public TopologySnapshot Build(
        TopologyDefinition definition,
        IReadOnlyDictionary<string, ServiceProbeResult> services,
        DateTimeOffset generatedAtUtc) {
        TopologyNodeSnapshot[] nodes = BuildNodeSnapshots(
            definition.Nodes,
            services,
            generatedAtUtc);

        TopologyEdgeSnapshot[] edges = BuildEdgeSnapshots(
            definition,
            nodes,
            services,
            generatedAtUtc);

        TopologyNodeSnapshot[] aggregateNodes = ApplyDependencyHealth(
            definition.Edges,
            nodes,
            edges);

        TopologyGroupSnapshot[] groups = BuildGroupSnapshots(
            definition.Groups,
            aggregateNodes);

        return new TopologySnapshot(
            generatedAtUtc,
            aggregateNodes,
            edges,
            groups);
    }

    /// <summary>
    /// Builds node snapshots before dependency health is applied.
    /// </summary>
    private static TopologyNodeSnapshot[] BuildNodeSnapshots(
        IReadOnlyList<TopologyNodeDefinition> nodeDefinitions,
        IReadOnlyDictionary<string, ServiceProbeResult> services,
        DateTimeOffset generatedAtUtc) {
        return nodeDefinitions
            .Select(node => BuildNodeSnapshot(
                node,
                services,
                generatedAtUtc))
            .ToArray();
    }

    /// <summary>
    /// Builds evaluated dependency-edge snapshots.
    /// </summary>
    private static TopologyEdgeSnapshot[] BuildEdgeSnapshots(
        TopologyDefinition definition,
        IReadOnlyList<TopologyNodeSnapshot> nodes,
        IReadOnlyDictionary<string, ServiceProbeResult> services,
        DateTimeOffset generatedAtUtc) {
        IReadOnlyDictionary<string, TopologyNodeDefinition>
            nodeDefinitionsById = definition.Nodes.ToDictionary(
                static node => node.Id,
                StringComparer.Ordinal);
        IReadOnlyDictionary<string, TopologyNodeSnapshot>
            nodeSnapshotsById = nodes.ToDictionary(
                static node => node.Id,
                StringComparer.Ordinal);

        return definition.Edges
            .Select(edge => BuildEdgeSnapshot(
                edge,
                services,
                nodeDefinitionsById,
                nodeSnapshotsById,
                generatedAtUtc))
            .ToArray();
    }

    /// <summary>
    /// Applies outgoing dependency health to each node snapshot.
    /// </summary>
    private TopologyNodeSnapshot[] ApplyDependencyHealth(
        IReadOnlyList<TopologyEdgeDefinition> edgeDefinitions,
        IReadOnlyList<TopologyNodeSnapshot> nodes,
        IReadOnlyList<TopologyEdgeSnapshot> edgeSnapshots) {
        ILookup<string, TopologyEdgeDefinition> definitionsBySource =
            edgeDefinitions.ToLookup(
                static edge => edge.SourceNodeId,
                StringComparer.Ordinal);
        ILookup<string, TopologyEdgeSnapshot> snapshotsBySource =
            edgeSnapshots.ToLookup(
                static edge => edge.SourceNodeId,
                StringComparer.Ordinal);

        return nodes
            .Select(node => ApplyDependencyHealth(
                node,
                definitionsBySource[node.Id].ToArray(),
                snapshotsBySource[node.Id].ToArray()))
            .ToArray();
    }

    /// <summary>
    /// Builds evaluated health snapshots for configured topology groups.
    /// </summary>
    private TopologyGroupSnapshot[] BuildGroupSnapshots(
        IReadOnlyList<TopologyGroupDefinition> groupDefinitions,
        IReadOnlyList<TopologyNodeSnapshot> aggregateNodes) {
        return groupDefinitions
            .Select(group => new TopologyGroupSnapshot(
                group.Id,
                groupHealthEvaluator.Evaluate(
                    group,
                    aggregateNodes)))
            .ToArray();
    }

    /// <summary>
    /// Builds the observation for one topology node.
    /// </summary>
    private static TopologyNodeSnapshot BuildNodeSnapshot(
        TopologyNodeDefinition node,
        IReadOnlyDictionary<string, ServiceProbeResult> services,
        DateTimeOffset checkedAtUtc) {
        ResourceAvailability? availability =
            node.Kind == TopologyNodeKind.Service
                ? ResolveAvailability(node.Id, services)
                : null;

        if (node.HealthSource is null
            || !services.TryGetValue(
                node.HealthSource.ProviderNodeId,
                out ServiceProbeResult? provider)) {
            return new TopologyNodeSnapshot(
                node.Id,
                availability,
                HealthStatus.Unknown,
                checkedAtUtc,
                Duration: null,
                "The node health source is unavailable.");
        }

        if (!provider.Health.Entries.TryGetValue(
                node.HealthSource.EntryKey,
                out HealthEntryProbeResult? entry)) {
            return new TopologyNodeSnapshot(
                node.Id,
                availability,
                HealthStatus.Unknown,
                provider.Health.CheckedAtUtc,
                Duration: null,
                $"Health entry '{node.HealthSource.EntryKey}' was not "
                    + "reported.");
        }

        return new TopologyNodeSnapshot(
            node.Id,
            availability,
            entry.Status,
            entry.CheckedAtUtc,
            entry.Duration,
            entry.Description);
    }

    /// <summary>
    /// Builds the evaluated observation for one dependency edge.
    /// </summary>
    private static TopologyEdgeSnapshot BuildEdgeSnapshot(
        TopologyEdgeDefinition edge,
        IReadOnlyDictionary<string, ServiceProbeResult> services,
        IReadOnlyDictionary<string, TopologyNodeDefinition>
            nodeDefinitionsById,
        IReadOnlyDictionary<string, TopologyNodeSnapshot>
            nodeSnapshotsById,
        DateTimeOffset checkedAtUtc) {
        if (!string.IsNullOrWhiteSpace(edge.HealthEntryKey)) {
            return BuildReportedEdgeSnapshot(
                edge,
                services,
                checkedAtUtc);
        }

        if (!nodeDefinitionsById.TryGetValue(
                edge.TargetNodeId,
                out TopologyNodeDefinition? targetDefinition)
            || targetDefinition.Kind != TopologyNodeKind.Service
            || !nodeSnapshotsById.TryGetValue(
                edge.TargetNodeId,
                out TopologyNodeSnapshot? targetSnapshot)) {
            return new TopologyEdgeSnapshot(
                edge.SourceNodeId,
                edge.TargetNodeId,
                HealthStatus.Unknown,
                checkedAtUtc,
                Duration: null,
                "No current target service observation is available.");
        }

        HealthStatus health = targetSnapshot.Availability switch {
            ResourceAvailability.Unavailable => HealthStatus.Unhealthy,
            ResourceAvailability.Unknown => HealthStatus.Unknown,
            ResourceAvailability.Available => targetSnapshot.Health,
            _ => HealthStatus.Unknown,
        };
        string? description =
            targetSnapshot.Availability == ResourceAvailability.Unavailable
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

    /// <summary>
    /// Builds an edge snapshot from an entry reported by the source service.
    /// </summary>
    private static TopologyEdgeSnapshot BuildReportedEdgeSnapshot(
        TopologyEdgeDefinition edge,
        IReadOnlyDictionary<string, ServiceProbeResult> services,
        DateTimeOffset checkedAtUtc) {
        if (!services.TryGetValue(
                edge.SourceNodeId,
                out ServiceProbeResult? source)) {
            return new TopologyEdgeSnapshot(
                edge.SourceNodeId,
                edge.TargetNodeId,
                HealthStatus.Unknown,
                checkedAtUtc,
                Duration: null,
                "The dependency source health report is unavailable.");
        }

        if (!source.Health.Entries.TryGetValue(
                edge.HealthEntryKey!,
                out HealthEntryProbeResult? entry)) {
            return new TopologyEdgeSnapshot(
                edge.SourceNodeId,
                edge.TargetNodeId,
                HealthStatus.Unknown,
                source.Health.CheckedAtUtc,
                Duration: null,
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

    /// <summary>
    /// Applies evaluated dependency health to one node observation.
    /// </summary>
    [SuppressMessage(
    "Performance",
    "CA1859:Use concrete types when possible for improved performance",
    Justification = "Prioritizing design clarity, encapsulation, and abstractions over micro-optimization.")]
    private TopologyNodeSnapshot ApplyDependencyHealth(
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
            CombineHealth(node.Health, dependencyHealth),
            node.CheckedAtUtc,
            node.Duration,
            node.Description);
    }

    /// <summary>
    /// Combines node health with evaluated dependency health.
    /// </summary>
    private static HealthStatus CombineHealth(
        HealthStatus nodeHealth,
        HealthStatus dependencyHealth) {
        if (nodeHealth == HealthStatus.Starting
            || dependencyHealth == HealthStatus.Starting) {
            return HealthStatus.Starting;
        }

        if (nodeHealth == HealthStatus.Unhealthy) {
            return HealthStatus.Unhealthy;
        }

        if (dependencyHealth == HealthStatus.Unhealthy) {
            return nodeHealth == HealthStatus.Healthy
                ? HealthStatus.Degraded
                : HealthStatus.Unhealthy;
        }

        if (nodeHealth == HealthStatus.Degraded
            || dependencyHealth == HealthStatus.Degraded) {
            return HealthStatus.Degraded;
        }

        if (nodeHealth == HealthStatus.Healthy) {
            return dependencyHealth == HealthStatus.Unknown
                ? HealthStatus.Degraded
                : HealthStatus.Healthy;
        }

        return HealthStatus.Unknown;
    }

    /// <summary>
    /// Resolves the observed availability for a service node.
    /// </summary>
    private static ResourceAvailability ResolveAvailability(
        string nodeId,
        IReadOnlyDictionary<string, ServiceProbeResult> services) {
        return services.TryGetValue(
            nodeId,
            out ServiceProbeResult? service)
                ? service.Availability.Availability
                : ResourceAvailability.Unknown;
    }
}
