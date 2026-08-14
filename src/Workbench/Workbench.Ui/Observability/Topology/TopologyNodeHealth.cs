namespace Workbench.Ui.Observability.Topology;

using global::Observability.Health;

/// <summary>
/// Represents a direct health observation for a topology node health source.
/// </summary>
/// <param name="Status">The observed health status.</param>
/// <param name="CheckedAtUtc">The time at which the health source was checked.</param>
/// <param name="Duration">The duration of the health check.</param>
/// <param name="Description">A sanitized explanation of the observed state.</param>
internal sealed record TopologyNodeHealth(
    HealthStatus Status,
    DateTimeOffset? CheckedAtUtc = null,
    TimeSpan? Duration = null,
    string? Description = null);
