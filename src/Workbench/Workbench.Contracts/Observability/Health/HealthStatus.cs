namespace Workbench.Contracts.Observability.Health;

/// <summary>
/// Represents the health state of a topology node or an aggregate of health observations.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Health has not yet been determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// Health is still being established and the observed resource is not yet ready.
    /// </summary>
    Starting,

    /// <summary>
    /// All observed parts are healthy.
    /// </summary>
    Healthy,

    /// <summary>
    /// At least part of the observed system remains available with reduced functionality.
    /// </summary>
    Degraded,

    /// <summary>
    /// No observed part of the system is available.
    /// </summary>
    Unhealthy,
}
