using Observability.Health;

namespace Observability.Topology.Snapshots;

/// <summary>
/// Represents evaluated health for a visual group.
/// </summary>
public sealed record TopologyGroupSnapshot(
    string Id,
    HealthStatus Health);