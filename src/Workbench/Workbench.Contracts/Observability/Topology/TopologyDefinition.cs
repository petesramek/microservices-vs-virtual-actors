namespace Workbench.Contracts.Observability.Topology;

/// <summary>
/// Defines the observable application topology.
/// </summary>
/// <param name="Root">The root node of the topology.</param>
public sealed record TopologyDefinition(
    TopologyNodeDefinition Root);
