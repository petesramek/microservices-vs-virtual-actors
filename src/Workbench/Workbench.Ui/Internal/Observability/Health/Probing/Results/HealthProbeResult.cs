namespace Workbench.Ui.Internal.Observability.Health.Probing.Results;

using global::Observability.Health;

/// <summary>
/// Represents the result of probing a service health endpoint.
/// </summary>
/// <param name="Status">The reported aggregate health status.</param>
/// <param name="CheckedAtUtc">The observation timestamp.</param>
/// <param name="Duration">The optional reported evaluation duration.</param>
/// <param name="Description">The optional explanatory description.</param>
/// <param name="Entries">Health entries indexed by stable entry key.</param>
internal sealed record HealthProbeResult(
    HealthStatus Status,
    DateTimeOffset CheckedAtUtc,
    TimeSpan? Duration,
    string? Description,
    IReadOnlyDictionary<string, HealthEntryProbeResult> Entries) {
    /// <summary>
    /// Creates an unavailable health result when no usable report can be
    /// collected.
    /// </summary>
    /// <param name="checkedAtUtc">The observation timestamp.</param>
    /// <param name="description">The failure description.</param>
    /// <returns>An unknown health result without reported entries.</returns>
    public static HealthProbeResult Unavailable(
        DateTimeOffset checkedAtUtc,
        string description) {
        return new HealthProbeResult(
            HealthStatus.Unknown,
            checkedAtUtc,
            Duration: null,
            description,
            new Dictionary<string, HealthEntryProbeResult>(
                StringComparer.Ordinal));
    }
}