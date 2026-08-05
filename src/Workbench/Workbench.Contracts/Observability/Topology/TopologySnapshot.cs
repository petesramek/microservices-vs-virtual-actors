namespace Workbench.Contracts.Observability.Topology;

/// <summary>
/// Represents a point-in-time view of the observable application topology.
/// </summary>
/// <param name="GeneratedAtUtc">The time at which the snapshot was generated.</param>
/// <param name="Nodes">The observed top-level nodes in display order.</param>
public sealed record TopologySnapshot(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<TopologyNodeSnapshot> Nodes);
