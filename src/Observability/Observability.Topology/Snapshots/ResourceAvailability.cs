namespace Observability.Topology.Snapshots;

/// <summary>
/// Represents whether a runnable service can be reached.
/// </summary>
public enum ResourceAvailability {
    /// <summary>
    /// Availability could not be determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The resource can be reached successfully.
    /// </summary>
    Available = 1,

    /// <summary>
    /// The resource could not be reached.
    /// </summary>
    Unavailable = 2,
}