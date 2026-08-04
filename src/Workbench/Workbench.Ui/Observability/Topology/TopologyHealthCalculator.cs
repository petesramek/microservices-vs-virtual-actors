namespace Workbench.Gateway.Observability.Topology;

using Workbench.Contracts.Observability.Health;
using Workbench.Contracts.Observability.Topology;

/// <summary>
/// Builds runtime topology snapshots and propagates dependency health from leaves to the root.
/// </summary>
internal sealed class TopologyHealthCalculator {
    /// <summary>
    /// Creates a point-in-time topology snapshot from a static definition and direct health observations.
    /// </summary>
    /// <param name="definition">The static topology definition.</param>
    /// <param name="healthBySource">Health observations keyed by topology health source.</param>
    /// <param name="generatedAtUtc">The time represented by the generated snapshot.</param>
    /// <returns>The generated topology snapshot.</returns>
    public TopologySnapshot Calculate(
        TopologyDefinition definition,
        IReadOnlyDictionary<string, TopologyNodeHealth> healthBySource,
        DateTimeOffset generatedAtUtc) {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(healthBySource);

        TopologyNodeSnapshot root = CalculateNode(
            definition.Root,
            healthBySource);

        return new TopologySnapshot(
            generatedAtUtc,
            root);
    }

    private static TopologyNodeSnapshot CalculateNode(
        TopologyNodeDefinition definition,
        IReadOnlyDictionary<string, TopologyNodeHealth> healthBySource) {
        TopologyNodeSnapshot[] children = definition.Children
            .Select(child => CalculateNode(child, healthBySource))
            .ToArray();

        TopologyNodeHealth ownHealth = ResolveOwnHealth(
            definition,
            healthBySource);

        HealthStatus compositeStatus = CalculateCompositeStatus(
            ownHealth.Status,
            definition.Children,
            children);

        return new TopologyNodeSnapshot(
            definition.Id,
            definition.DisplayName,
            definition.Kind,
            ownHealth.Status,
            compositeStatus,
            ownHealth.CheckedAtUtc,
            ownHealth.Duration,
            ownHealth.Description,
            children);
    }

    private static TopologyNodeHealth ResolveOwnHealth(
        TopologyNodeDefinition definition,
        IReadOnlyDictionary<string, TopologyNodeHealth> healthBySource) {
        if (definition.HealthSource is null) {
            return new TopologyNodeHealth(
                HealthStatus.Healthy);
        }

        return healthBySource.TryGetValue(
            definition.HealthSource,
            out TopologyNodeHealth? health)
                ? health
                : new TopologyNodeHealth(
                    HealthStatus.Unknown,
                    Description: "No health observation is available.");
    }

    private static HealthStatus CalculateCompositeStatus(
        HealthStatus ownStatus,
        IReadOnlyList<TopologyNodeDefinition> childDefinitions,
        IReadOnlyList<TopologyNodeSnapshot> childSnapshots) {
        HealthStatus compositeStatus = ownStatus;

        for (int index = 0; index < childSnapshots.Count; index++) {
            HealthStatus childStatus = ApplyRequirement(
                childSnapshots[index].CompositeStatus,
                childDefinitions[index].Requirement);

            if (GetSeverity(childStatus) > GetSeverity(compositeStatus)) {
                compositeStatus = childStatus;
            }
        }

        return compositeStatus;
    }

    private static HealthStatus ApplyRequirement(
        HealthStatus status,
        TopologyDependencyRequirement requirement) {
        if (requirement != TopologyDependencyRequirement.Optional) {
            return status;
        }

        return status switch {
            HealthStatus.Unhealthy =>
                HealthStatus.Degraded,
            _ => status,
        };
    }

    private static int GetSeverity(HealthStatus status) {
        return status switch {
            HealthStatus.Healthy => 0,
            HealthStatus.Unknown => 1,
            HealthStatus.Starting => 2,
            HealthStatus.Degraded => 3,
            HealthStatus.Unhealthy => 4,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unsupported observability health status."),
        };
    }
}
