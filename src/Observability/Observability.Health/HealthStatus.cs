namespace Observability.Health;

using System.Text.Json.Serialization;

/// <summary>
/// Defines the health state of a resource or an aggregate health observation.
/// </summary>
/// <remarks>
/// Values are serialized as their names to keep the cross-API JSON contract
/// readable and independent of the underlying numeric representation.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HealthStatus {
    /// <summary>
    /// Indicates that health has not been determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Indicates that the resource is still establishing readiness.
    /// </summary>
    Starting = 1,

    /// <summary>
    /// Indicates that the resource or aggregate is operating normally.
    /// </summary>
    Healthy = 2,

    /// <summary>
    /// Indicates that the resource or aggregate remains available with reduced
    /// health or functionality.
    /// </summary>
    Degraded = 3,

    /// <summary>
    /// Indicates that the resource or aggregate is not healthy enough to serve
    /// its intended purpose.
    /// </summary>
    Unhealthy = 4,
}
