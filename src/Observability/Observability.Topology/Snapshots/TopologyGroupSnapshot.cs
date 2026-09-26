using Observability.Health;

namespace Observability.Topology.Snapshots;

/// <summary>
/// Represents the aggregate point-in-time health of a topology group.
/// </summary>
/// <param name="Id">
/// The stable identifier of the group represented by the snapshot.
/// </param>
/// <param name="Health">
/// The health aggregated from the group's member node snapshots.
/// </param>
/// <remarks>
/// Group membership is defined by the corresponding topology group definition
/// and is not duplicated in this snapshot.
/// </remarks>
public sealed record TopologyGroupSnapshot(
    string Id,
    HealthStatus Health);
