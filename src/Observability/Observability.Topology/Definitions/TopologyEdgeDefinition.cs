namespace Observability.Topology.Definitions;

/// <summary>
/// Defines a dependency relationship between two nodes.
/// </summary>
public sealed record TopologyEdgeDefinition(
    string SourceNodeId,
    string TargetNodeId,
    TopologyDependencyRequirement Requirement,
    string? HealthEntryKey);