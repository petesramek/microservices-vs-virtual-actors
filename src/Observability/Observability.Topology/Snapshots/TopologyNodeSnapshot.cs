using Observability.Health;

namespace Observability.Topology.Snapshots;

/// <summary>
/// Represents direct runtime observations for a node.
/// </summary>
public sealed record TopologyNodeSnapshot(
    string Id,
    ResourceAvailability? Availability,
    HealthStatus Health,
    DateTimeOffset? CheckedAtUtc,
    TimeSpan? Duration,
    string? Description);