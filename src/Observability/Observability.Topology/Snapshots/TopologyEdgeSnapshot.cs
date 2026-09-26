using Observability.Health;

namespace Observability.Topology.Snapshots;

/// <summary>
/// Represents a point-in-time health observation for a directed topology
/// dependency.
/// </summary>
/// <param name="SourceNodeId">
/// The stable identifier of the node that owns the dependency.
/// </param>
/// <param name="TargetNodeId">
/// The stable identifier of the node on which the source depends.
/// </param>
/// <param name="Health">
/// The health reported for the dependency.
/// </param>
/// <param name="CheckedAtUtc">
/// The UTC timestamp of the observation, or <see langword="null"/> when no
/// observation time is available.
/// </param>
/// <param name="Duration">
/// The non-negative check duration, or <see langword="null"/> when no duration
/// is available.
/// </param>
/// <param name="Description">
/// An optional, non-sensitive description of the dependency observation.
/// </param>
/// <remarks>
/// Dependency direction is from <paramref name="SourceNodeId"/> to
/// <paramref name="TargetNodeId"/>. Identifier validity and edge uniqueness are
/// enforced by the topology validation and snapshot-producing pipeline.
/// </remarks>
public sealed record TopologyEdgeSnapshot(
    string SourceNodeId,
    string TargetNodeId,
    HealthStatus Health,
    DateTimeOffset? CheckedAtUtc,
    TimeSpan? Duration,
    string? Description);
