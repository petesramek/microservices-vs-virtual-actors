namespace Observability.Topology;

/// <summary>
/// Defines the observable application topology.
/// </summary>
/// <param name="Nodes">The top-level topology nodes in display order.</param>
public sealed record TopologyDefinition(
    IReadOnlyList<TopologyNodeDefinition> Nodes);
