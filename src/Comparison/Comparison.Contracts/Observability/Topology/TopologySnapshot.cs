namespace Comparison.Contracts.Observability.Topology;

/// <summary>
/// Represents a point-in-time view of the observable application topology.
/// </summary>
/// <param name="GeneratedAtUtc">The time at which the snapshot was generated.</param>
/// <param name="Root">The observed root node.</param>
public sealed record TopologySnapshot(
    DateTimeOffset GeneratedAtUtc,
    TopologyNodeSnapshot Root);
