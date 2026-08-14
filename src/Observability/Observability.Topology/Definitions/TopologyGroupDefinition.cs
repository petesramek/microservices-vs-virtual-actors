namespace Observability.Topology.Definitions;

/// <summary>
/// Defines a visual and aggregation group.
/// </summary>
public sealed record TopologyGroupDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<string> NodeIds);