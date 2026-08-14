using Observability.Health;
using Observability.Topology.Definitions;
using Observability.Topology.Snapshots;

namespace Observability.Topology.Evaluation;

/// <summary>
/// Evaluates aggregate group health.
/// </summary>
public sealed class GroupHealthEvaluator {
    private readonly HealthStatusEvaluator _healthStatusEvaluator;

    public GroupHealthEvaluator(
        HealthStatusEvaluator healthStatusEvaluator) {
        _healthStatusEvaluator = healthStatusEvaluator;
    }

    /// <summary>
    /// Evaluates a group snapshot.
    /// </summary>
    public HealthStatus Evaluate(
        TopologyGroupDefinition group,
        IReadOnlyCollection<TopologyNodeSnapshot> nodes) {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(nodes);

        var memberStatuses =
            group.NodeIds
                .Select(nodeId =>
                    nodes.FirstOrDefault(x => x.Id == nodeId)?.Health
                    ?? HealthStatus.Unknown)
                .ToArray();

        return _healthStatusEvaluator.Evaluate(memberStatuses);
    }
}