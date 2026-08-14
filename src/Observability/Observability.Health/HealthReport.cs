namespace Observability.Health;
/// <summary>
/// Represents the detailed health report of one application.
/// </summary>
/// <param name="Status">The aggregate application health status.</param>
/// <param name="DurationMilliseconds">The total health-check duration in milliseconds.</param>
/// <param name="Entries">The named health-check results.</param>
public sealed record HealthReport(
    HealthStatus Status,
    long DurationMilliseconds,
    IReadOnlyDictionary<string, HealthEntry> Entries);
