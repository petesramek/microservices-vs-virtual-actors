namespace Observability.Topology.Definitions;

/// <summary>
/// Defines a service or storage resource in the topology.
/// </summary>
public sealed record TopologyNodeDefinition(
    string Id,
    string DisplayName,
    TopologyNodeKind Kind,
    HealthSourceDefinition? HealthSource);