namespace Observability.Topology.Evaluators.Abstraction;

using Observability.Health;
using Observability.Topology.Definitions;
using Observability.Topology.Snapshots;

/// <summary>
/// Defines a service that evaluates aggregate health for topology dependencies.
/// </summary>
public interface IDependencyHealthEvaluator {
    /// <summary>
    /// Evaluates aggregate health for the supplied dependency definitions and
    /// their current snapshots.
    /// </summary>
    /// <param name="edgeDefinitions">
    /// The directed dependency definitions to evaluate.
    /// </param>
    /// <param name="edgeSnapshots">
    /// The current dependency snapshots matched directionally by source and
    /// target node identifier.
    /// </param>
    /// <returns>
    /// The aggregate dependency health, or <see cref="HealthStatus.Unknown"/>
    /// when no dependency definitions are supplied.
    /// </returns>
    /// <remarks>
    /// Implementations define how missing snapshots and dependency requirements
    /// contribute to the aggregate health result.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="edgeDefinitions"/> or <paramref name="edgeSnapshots"/>
    /// is <see langword="null"/>.
    /// </exception>
    HealthStatus Evaluate(
        IReadOnlyCollection<TopologyEdgeDefinition> edgeDefinitions,
        IReadOnlyCollection<TopologyEdgeSnapshot> edgeSnapshots);
}
