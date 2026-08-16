namespace Observability.Health;

using System.Collections.ObjectModel;

/// <summary>
/// Represents the aggregate health report for one application.
/// </summary>
/// <remarks>
/// The producer supplies the aggregate status according to its health policy;
/// this contract does not recalculate it from individual entries.
/// </remarks>
public sealed record HealthReport {
    /// <summary>
    /// Initializes a new instance of the <see cref="HealthReport"/> class.
    /// </summary>
    /// <param name="status">The aggregate application health status.</param>
    /// <param name="durationMilliseconds">
    /// The total health-check execution duration in milliseconds.
    /// </param>
    /// <param name="entries">
    /// The health-check results keyed by their registered check names.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="entries"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="durationMilliseconds"/> is negative.
    /// </exception>
    public HealthReport(
        HealthStatus status,
        long durationMilliseconds,
        IReadOnlyDictionary<string, HealthEntry> entries) {
        ArgumentOutOfRangeException.ThrowIfNegative(durationMilliseconds);
        ArgumentNullException.ThrowIfNull(entries);

        Status = status;
        DurationMilliseconds = durationMilliseconds;
        Entries = new ReadOnlyDictionary<string, HealthEntry>(
            new Dictionary<string, HealthEntry>(
                entries,
                StringComparer.Ordinal));
    }

    /// <summary>
    /// Gets the aggregate application health status.
    /// </summary>
    public HealthStatus Status { get; }

    /// <summary>
    /// Gets the total health-check execution duration in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; }

    /// <summary>
    /// Gets a snapshot of the health-check results keyed by their registered
    /// check names.
    /// </summary>
    public IReadOnlyDictionary<string, HealthEntry> Entries { get; }
}
