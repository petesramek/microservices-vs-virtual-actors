namespace Comparison.Gateway.Observability.Topology;

using Comparison.Contracts.Observability.Health;
using Comparison.Contracts.Observability.Topology;

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

        ObservabilityHealthStatus compositeStatus = CalculateCompositeStatus(
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
                ObservabilityHealthStatus.Healthy);
        }

        return healthBySource.TryGetValue(
            definition.HealthSource,
            out TopologyNodeHealth? health)
                ? health
                : new TopologyNodeHealth(
                    ObservabilityHealthStatus.Unknown,
                    Description: "No health observation is available.");
    }

    private static ObservabilityHealthStatus CalculateCompositeStatus(
        ObservabilityHealthStatus ownStatus,
        IReadOnlyList<TopologyNodeDefinition> childDefinitions,
        IReadOnlyList<TopologyNodeSnapshot> childSnapshots) {
        ObservabilityHealthStatus compositeStatus = ownStatus;

        for (int index = 0; index < childSnapshots.Count; index++) {
            ObservabilityHealthStatus childStatus = ApplyRequirement(
                childSnapshots[index].CompositeStatus,
                childDefinitions[index].Requirement);

            if (GetSeverity(childStatus) > GetSeverity(compositeStatus)) {
                compositeStatus = childStatus;
            }
        }

        return compositeStatus;
    }

    private static ObservabilityHealthStatus ApplyRequirement(
        ObservabilityHealthStatus status,
        TopologyDependencyRequirement requirement) {
        if (requirement != TopologyDependencyRequirement.Optional) {
            return status;
        }

        return status switch {
            ObservabilityHealthStatus.Unhealthy =>
                ObservabilityHealthStatus.Degraded,
            _ => status,
        };
    }

    private static int GetSeverity(ObservabilityHealthStatus status) {
        return status switch {
            ObservabilityHealthStatus.Healthy => 0,
            ObservabilityHealthStatus.Unknown => 1,
            ObservabilityHealthStatus.Starting => 2,
            ObservabilityHealthStatus.Degraded => 3,
            ObservabilityHealthStatus.Unhealthy => 4,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unsupported observability health status."),
        };
    }
}
