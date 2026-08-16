namespace Observability.Topology.Snapshots;

/// <summary>
/// Represents whether a runtime resource can be reached.
/// </summary>
/// <remarks>
/// Availability describes reachability and is independent of the resource's
/// reported health status.
/// </remarks>
public enum ResourceAvailability {
    /// <summary>
    /// Indicates that resource availability has not been determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Indicates that the resource was reached successfully.
    /// </summary>
    Available = 1,

    /// <summary>
    /// Indicates that the resource could not be reached.
    /// </summary>
    Unavailable = 2,
}
