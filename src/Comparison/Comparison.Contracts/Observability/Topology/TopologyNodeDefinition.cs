namespace Comparison.Contracts.Observability.Topology;

/// <summary>
/// Defines a node in the observable application topology.
/// </summary>
/// <param name="Id">The stable identifier of the node.</param>
/// <param name="DisplayName">The display name shown to users.</param>
/// <param name="Kind">The role of the node in the topology.</param>
/// <param name="HealthSource">The resource name or health-check key that supplies the node health.</param>
/// <param name="Requirement">How the node affects the composite health of its parent.</param>
/// <param name="Children">The direct dependencies of the node.</param>
public sealed record TopologyNodeDefinition(
    string Id,
    string DisplayName,
    TopologyNodeKind Kind,
    string? HealthSource,
    TopologyDependencyRequirement Requirement,
    IReadOnlyList<TopologyNodeDefinition> Children);
