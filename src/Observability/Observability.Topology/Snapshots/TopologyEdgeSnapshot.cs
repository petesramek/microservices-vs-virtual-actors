using Observability.Health;

namespace Observability.Topology.Snapshots;

/// <summary>
/// Represents point-in-time dependency health observations.
/// </summary>
public sealed record TopologyEdgeSnapshot(
    string SourceNodeId,
    string TargetNodeId,
    HealthStatus Health,
    DateTimeOffset? CheckedAtUtc,
    TimeSpan? Duration,
    string? Description);