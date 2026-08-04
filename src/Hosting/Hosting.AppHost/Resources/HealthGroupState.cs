namespace Hosting.AppHost.Resources;

/// <summary>
/// Defines the aggregate states displayed for an Aspire health group.
/// </summary>
internal enum HealthGroupState {
    /// <summary>
    /// Child resource states are not yet available.
    /// </summary>
    Unknown,

    /// <summary>
    /// At least one child resource is still starting or waiting.
    /// </summary>
    Starting,

    /// <summary>
    /// All child resources are healthy.
    /// </summary>
    Healthy,

    /// <summary>
    /// At least one child resource is healthy and at least one is unhealthy.
    /// </summary>
    Degraded,

    /// <summary>
    /// No child resource is healthy.
    /// </summary>
    Unhealthy,
}
