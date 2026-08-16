namespace Hosting.AppHost.Resources;

/// <summary>
/// Defines the aggregate states displayed for an Aspire health group.
/// </summary>
internal enum HealthGroupState
{
    /// <summary>
    /// Indicates that child resource health is not yet available or cannot be
    /// determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// Indicates that at least one child resource is still starting or waiting.
    /// </summary>
    Starting,

    /// <summary>
    /// Indicates that all child resources are healthy.
    /// </summary>
    Healthy,

    /// <summary>
    /// Indicates that the aggregate child health is degraded.
    /// </summary>
    Degraded,

    /// <summary>
    /// Indicates that the aggregate child health is unhealthy.
    /// </summary>
    Unhealthy,
}
