namespace Observability.Topology.Definitions;

/// <summary>
/// Defines a directed dependency relationship between two topology nodes.
/// </summary>
/// <param name="SourceNodeId">
/// The stable identifier of the node that owns the dependency.
/// </param>
/// <param name="TargetNodeId">
/// The stable identifier of the node on which the source depends.
/// </param>
/// <param name="Requirement">
/// The availability requirement applied to the dependency.
/// </param>
/// <param name="HealthEntryKey">
/// The optional health-report entry emitted by the source for this dependency.
/// </param>
/// <remarks>
/// The edge is directional from <paramref name="SourceNodeId"/> to
/// <paramref name="TargetNodeId"/>. Graph invariants, including endpoint
/// existence, self-dependencies, and duplicate edges, are enforced by the
/// topology validator rather than by this transport contract.
/// </remarks>
public sealed record TopologyEdgeDefinition(
    string SourceNodeId,
    string TargetNodeId,
    TopologyDependencyRequirement Requirement,
    string? HealthEntryKey);
