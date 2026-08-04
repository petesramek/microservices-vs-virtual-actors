namespace Workbench.Contracts.Observability.Health;

/// <summary>
/// Represents the health state of a topology node.
/// </summary>
public enum ObservabilityHealthStatus {
    /// <summary>
    /// The node health has not yet been determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// The node is starting and is not yet ready.
    /// </summary>
    Starting,

    /// <summary>
    /// The node and its required dependencies are healthy.
    /// </summary>
    Healthy,

    /// <summary>
    /// The node remains available with reduced functionality.
    /// </summary>
    Degraded,

    /// <summary>
    /// The node is unavailable or a required dependency is unhealthy.
    /// </summary>
    Unhealthy,
}
