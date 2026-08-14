using Observability.Health;
using Observability.Topology.Definitions;
using Observability.Topology.Snapshots;

namespace Observability.Topology.Evaluation;

/// <summary>
/// Evaluates dependency health from graph edges.
/// </summary>
public sealed class DependencyHealthEvaluator {
    private readonly HealthStatusEvaluator _healthStatusEvaluator;

    public DependencyHealthEvaluator(
        HealthStatusEvaluator healthStatusEvaluator) {
        _healthStatusEvaluator = healthStatusEvaluator;
    }

    /// <summary>
    /// Evaluates overall dependency health.
    /// </summary>
    public HealthStatus Evaluate(
        IReadOnlyCollection<TopologyEdgeDefinition> edgeDefinitions,
        IReadOnlyCollection<TopologyEdgeSnapshot> edgeSnapshots) {
        ArgumentNullException.ThrowIfNull(edgeDefinitions);
        ArgumentNullException.ThrowIfNull(edgeSnapshots);

        if (edgeDefinitions.Count == 0) {
            return HealthStatus.Unknown;
        }

        var statuses = new List<HealthStatus>();

        foreach (var definition in edgeDefinitions) {
            var snapshot = edgeSnapshots.FirstOrDefault(
                x =>
                    x.SourceNodeId == definition.SourceNodeId &&
                    x.TargetNodeId == definition.TargetNodeId);

            if (snapshot is null) {
                statuses.Add(HealthStatus.Unknown);
                continue;
            }

            if (definition.Requirement ==
                TopologyDependencyRequirement.Optional &&
                snapshot.Health == HealthStatus.Unhealthy) {
                statuses.Add(HealthStatus.Degraded);
                continue;
            }

            statuses.Add(snapshot.Health);
        }

        return _healthStatusEvaluator.Evaluate(statuses);
    }
}