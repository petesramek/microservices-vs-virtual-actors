namespace Workbench.Contracts.Observability.Health;

/// <summary>
/// Represents the result of one named health check.
/// </summary>
/// <param name="Status">The health status reported by the check.</param>
/// <param name="Description">The safe diagnostic description, when available.</param>
/// <param name="DurationMilliseconds">The health-check duration in milliseconds.</param>
public sealed record HealthEntry(
    HealthStatus Status,
    string? Description,
    long DurationMilliseconds);
