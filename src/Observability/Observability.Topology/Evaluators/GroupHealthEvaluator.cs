using Observability.Health;
using Observability.Health.Abstraction;
using Observability.Topology.Definitions;
using Observability.Topology.Evaluators.Abstraction;
using Observability.Topology.Snapshots;

namespace Observability.Topology.Evaluators;

/// <summary>
/// Evaluates aggregate health for topology groups.
/// </summary>
/// <remarks>
/// Every configured group member contributes one health observation. A member
/// without a matching node snapshot contributes <see cref="HealthStatus.Unknown"/>.
/// Member observations are aggregated by the configured
/// <see cref="IHealthStatusEvaluator"/>.
/// </remarks>
public sealed class GroupHealthEvaluator : IGroupHealthEvaluator {
    /// <summary>
    /// Provides the shared health-status aggregation policy.
    /// </summary>
    private readonly IHealthStatusEvaluator _healthStatusEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupHealthEvaluator"/>
    /// class.
    /// </summary>
    /// <param name="healthStatusEvaluator">
    /// The evaluator used to aggregate group-member health observations.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="healthStatusEvaluator"/> is <see langword="null"/>.
    /// </exception>
    public GroupHealthEvaluator(
        IHealthStatusEvaluator healthStatusEvaluator) {
        ArgumentNullException.ThrowIfNull(healthStatusEvaluator);
        _healthStatusEvaluator = healthStatusEvaluator;
    }

    /// <summary>
    /// Evaluates aggregate health for a topology group.
    /// </summary>
    /// <param name="group">The group definition to evaluate.</param>
    /// <param name="nodes">The current topology node snapshots.</param>
    /// <returns>
    /// The aggregate member health, or <see cref="HealthStatus.Unknown"/> when
    /// the group contains no members.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="group"/> or <paramref name="nodes"/> is
    /// <see langword="null"/>.
    /// </exception>
    public HealthStatus Evaluate(
        TopologyGroupDefinition group,
        IReadOnlyCollection<TopologyNodeSnapshot> nodes) {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(nodes);

        Dictionary<string, HealthStatus> healthByNode =
            CreateHealthIndex(nodes);
        var memberStatuses = new HealthStatus[group.NodeIds.Count];

        for (int index = 0; index < group.NodeIds.Count; index++) {
            string nodeId = group.NodeIds[index];
            memberStatuses[index] = healthByNode.GetValueOrDefault(
                nodeId,
                HealthStatus.Unknown);
        }

        return _healthStatusEvaluator.Evaluate(memberStatuses);
    }

    /// <summary>
    /// Builds a case-sensitive lookup of node health by node ID.
    /// </summary>
    /// <param name="nodes">The node snapshots to index.</param>
    /// <returns>A lookup containing the first snapshot for each node ID.</returns>
    /// <remarks>
    /// Retaining the first snapshot preserves first-match behavior if duplicate
    /// snapshots are supplied. Snapshot uniqueness remains a responsibility of
    /// the producing pipeline.
    /// </remarks>
    private static Dictionary<string, HealthStatus> CreateHealthIndex(
        IReadOnlyCollection<TopologyNodeSnapshot> nodes) {
        var healthByNode = new Dictionary<string, HealthStatus>(
            StringComparer.Ordinal);

        foreach (TopologyNodeSnapshot node in nodes) {
            healthByNode.TryAdd(node.Id, node.Health);
        }

        return healthByNode;
    }
}
