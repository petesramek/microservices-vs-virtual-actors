using Observability.Health;

namespace Observability.Topology.Snapshots;

/// <summary>
/// Represents direct point-in-time runtime observations for a topology node.
/// </summary>
/// <param name="Id">
/// The stable identifier of the node represented by the snapshot.
/// </param>
/// <param name="Availability">
/// The observed reachability of the runtime resource, or
/// <see langword="null"/> when availability is not applicable.
/// </param>
/// <param name="Health">
/// The direct health reported for the node.
/// </param>
/// <param name="CheckedAtUtc">
/// The UTC timestamp of the health observation, or <see langword="null"/> when
/// no observation time is available.
/// </param>
/// <param name="Duration">
/// The non-negative health-check duration, or <see langword="null"/> when no
/// duration is available.
/// </param>
/// <param name="Description">
/// An optional, non-sensitive description of the node observation.
/// </param>
/// <remarks>
/// Availability describes reachability and is independent of
/// <paramref name="Health"/>. A reachable node can report degraded or unhealthy
/// status. Identifier validity and snapshot uniqueness are responsibilities of
/// the topology validation and snapshot-producing pipeline.
/// </remarks>
public sealed record TopologyNodeSnapshot(
    string Id,
    ResourceAvailability? Availability,
    HealthStatus Health,
    DateTimeOffset? CheckedAtUtc,
    TimeSpan? Duration,
    string? Description);
