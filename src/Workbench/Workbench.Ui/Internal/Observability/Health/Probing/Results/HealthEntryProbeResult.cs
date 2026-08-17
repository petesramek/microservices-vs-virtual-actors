namespace Workbench.Ui.Internal.Observability.Health.Probing.Results;

using global::Observability.Health;


/// <summary>
/// Represents one entry reported by a service health endpoint.
/// </summary>
/// <param name="Status">The reported entry status.</param>
/// <param name="CheckedAtUtc">The observation timestamp.</param>
/// <param name="Duration">The optional reported evaluation duration.</param>
/// <param name="Description">The optional explanatory description.</param>
internal sealed record HealthEntryProbeResult(
    HealthStatus Status,
    DateTimeOffset CheckedAtUtc,
    TimeSpan? Duration,
    string? Description);