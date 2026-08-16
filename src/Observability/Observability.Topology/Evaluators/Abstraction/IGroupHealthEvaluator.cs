namespace Observability.Topology.Evaluators.Abstraction;

using Observability.Health;
using Observability.Topology.Definitions;
using Observability.Topology.Snapshots;

/// <summary>
/// Defines a service that evaluates aggregate health for a topology group.
/// </summary>
public interface IGroupHealthEvaluator {
    /// <summary>
    /// Evaluates aggregate health for the supplied topology group.
    /// </summary>
    /// <param name="group">The group definition to evaluate.</param>
    /// <param name="nodes">
    /// The current topology node snapshots matched to group members by node
    /// identifier.
    /// </param>
    /// <returns>
    /// The aggregate health of the group's member nodes, or
    /// <see cref="HealthStatus.Unknown"/> when the group contains no members.
    /// </returns>
    /// <remarks>
    /// Implementations define how members without matching node snapshots
    /// contribute to the aggregate health result.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="group"/> or <paramref name="nodes"/> is
    /// <see langword="null"/>.
    /// </exception>
    HealthStatus Evaluate(
        TopologyGroupDefinition group,
        IReadOnlyCollection<TopologyNodeSnapshot> nodes);
}
