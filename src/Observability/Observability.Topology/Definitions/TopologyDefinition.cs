namespace Observability.Topology.Definitions;

/// <summary>
/// Defines the complete static observability graph.
/// </summary>
public sealed record TopologyDefinition(
    IReadOnlyList<TopologyNodeDefinition> Nodes,
    IReadOnlyList<TopologyEdgeDefinition> Edges,
    IReadOnlyList<TopologyGroupDefinition> Groups);