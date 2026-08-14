namespace Observability.Topology.Snapshots;

/// <summary>
/// Represents a point-in-time observability snapshot.
/// </summary>
public sealed record TopologySnapshot(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<TopologyNodeSnapshot> Nodes,
    IReadOnlyList<TopologyEdgeSnapshot> Edges,
    IReadOnlyList<TopologyGroupSnapshot> Groups);