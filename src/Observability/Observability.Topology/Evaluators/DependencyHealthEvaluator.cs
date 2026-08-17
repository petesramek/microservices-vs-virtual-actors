using Observability.Health;
using Observability.Health.Abstraction;
using Observability.Topology.Definitions;
using Observability.Topology.Evaluators.Abstraction;
using Observability.Topology.Snapshots;

namespace Observability.Topology.Evaluators;

/// <summary>
/// Evaluates aggregate dependency health from topology edge definitions and
/// their current snapshots.
/// </summary>
/// <remarks>
/// A missing edge snapshot contributes <see cref="HealthStatus.Unknown"/>. An
/// unhealthy optional dependency contributes <see cref="HealthStatus.Degraded"/>
/// instead of <see cref="HealthStatus.Unhealthy"/>. All resulting observations
/// are aggregated by the configured <see cref="IHealthStatusEvaluator"/>.
/// </remarks>
public sealed class DependencyHealthEvaluator : IDependencyHealthEvaluator {
    /// <summary>
    /// Provides the shared health-status aggregation policy.
    /// </summary>
    private readonly IHealthStatusEvaluator _healthStatusEvaluator;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="DependencyHealthEvaluator"/> class.
    /// </summary>
    /// <param name="healthStatusEvaluator">
    /// The evaluator used to aggregate dependency health observations.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="healthStatusEvaluator"/> is <see langword="null"/>.
    /// </exception>
    public DependencyHealthEvaluator(
        IHealthStatusEvaluator healthStatusEvaluator) {
        ArgumentNullException.ThrowIfNull(healthStatusEvaluator);
        _healthStatusEvaluator = healthStatusEvaluator;
    }

    /// <summary>
    /// Evaluates aggregate health for the supplied dependency definitions.
    /// </summary>
    /// <param name="edgeDefinitions">
    /// The directed dependency definitions to evaluate.
    /// </param>
    /// <param name="edgeSnapshots">
    /// The current dependency snapshots matched by source and target node ID.
    /// </param>
    /// <returns>
    /// The aggregate dependency health, or <see cref="HealthStatus.Unknown"/>
    /// when no dependency definitions are supplied.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="edgeDefinitions"/> or <paramref name="edgeSnapshots"/>
    /// is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A dependency definition contains an unsupported
    /// <see cref="TopologyDependencyRequirement"/> value.
    /// </exception>
    public HealthStatus Evaluate(
        IReadOnlyCollection<TopologyEdgeDefinition> edgeDefinitions,
        IReadOnlyCollection<TopologyEdgeSnapshot> edgeSnapshots) {
        ArgumentNullException.ThrowIfNull(edgeDefinitions);
        ArgumentNullException.ThrowIfNull(edgeSnapshots);

        if (edgeDefinitions.Count == 0) {
            return HealthStatus.Unknown;
        }

        Dictionary<(string SourceNodeId, string TargetNodeId), HealthStatus>
            healthByEdge = CreateHealthIndex(edgeSnapshots);
        var statuses = new List<HealthStatus>(edgeDefinitions.Count);

        foreach (TopologyEdgeDefinition definition in edgeDefinitions) {
            if (!healthByEdge.TryGetValue(
                    (definition.SourceNodeId, definition.TargetNodeId),
                    out HealthStatus health)) {
                statuses.Add(HealthStatus.Unknown);
                continue;
            }

            statuses.Add(ApplyRequirement(
                health,
                definition.Requirement));
        }

        return _healthStatusEvaluator.Evaluate(statuses);
    }

    /// <summary>
    /// Builds a lookup of dependency health by directional source-target pair.
    /// </summary>
    /// <param name="snapshots">The snapshots to index.</param>
    /// <returns>A lookup containing the first snapshot for each edge.</returns>
    /// <remarks>
    /// Retaining the first snapshot preserves first-match behavior if duplicate
    /// snapshots are supplied. Snapshot uniqueness remains a responsibility of
    /// the producing pipeline.
    /// </remarks>
    private static Dictionary<(string SourceNodeId, string TargetNodeId), HealthStatus>
        CreateHealthIndex(
            IReadOnlyCollection<TopologyEdgeSnapshot> snapshots) {
        var healthByEdge = new Dictionary<
            (string SourceNodeId, string TargetNodeId),
            HealthStatus>();

        foreach (TopologyEdgeSnapshot snapshot in snapshots) {
            healthByEdge.TryAdd(
                (snapshot.SourceNodeId, snapshot.TargetNodeId),
                snapshot.Health);
        }

        return healthByEdge;
    }

    /// <summary>
    /// Applies a dependency requirement to an observed dependency status.
    /// </summary>
    /// <param name="health">The observed dependency health.</param>
    /// <param name="requirement">The dependency availability requirement.</param>
    /// <returns>The health contribution used for aggregate evaluation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="requirement"/> is unsupported.
    /// </exception>
    private static HealthStatus ApplyRequirement(
        HealthStatus health,
        TopologyDependencyRequirement requirement) {
        return requirement switch {
            TopologyDependencyRequirement.Required => health,
            TopologyDependencyRequirement.Optional
                when health == HealthStatus.Unhealthy => HealthStatus.Degraded,
            TopologyDependencyRequirement.Optional => health,
            _ => throw new ArgumentOutOfRangeException(
                nameof(requirement),
                requirement,
                "Unsupported topology dependency requirement."),
        };
    }
}
