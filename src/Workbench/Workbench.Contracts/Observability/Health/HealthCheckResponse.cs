namespace Workbench.Contracts.Observability.Health;

/// <summary>
/// Represents the detailed health response of one application resource.
/// </summary>
/// <param name="Status">The aggregate application health status.</param>
/// <param name="DurationMilliseconds">The total health-check duration in milliseconds.</param>
/// <param name="Entries">The named health-check results.</param>
public sealed record HealthCheckResponse(
    HealthStatus Status,
    long DurationMilliseconds,
    IReadOnlyDictionary<string, HealthCheckEntry> Entries);
