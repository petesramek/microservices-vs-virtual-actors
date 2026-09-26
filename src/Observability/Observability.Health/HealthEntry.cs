namespace Observability.Health;

/// <summary>
/// Represents the result of one named health check.
/// </summary>
/// <remarks>
/// The containing <see cref="HealthReport"/> supplies the name that identifies
/// this entry. Diagnostic descriptions may be exposed by a health endpoint and
/// therefore should not contain secrets or sensitive implementation details.
/// </remarks>
public sealed record HealthEntry {
    /// <summary>
    /// Initializes a new instance of the <see cref="HealthEntry"/> class.
    /// </summary>
    /// <param name="status">The status reported by the health check.</param>
    /// <param name="description">
    /// An optional, non-sensitive diagnostic description of the result.
    /// </param>
    /// <param name="durationMilliseconds">
    /// The health-check execution duration in milliseconds.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="durationMilliseconds"/> is negative.
    /// </exception>
    public HealthEntry(
        HealthStatus status,
        string? description,
        long durationMilliseconds) {
        ArgumentOutOfRangeException.ThrowIfNegative(durationMilliseconds);

        Status = status;
        Description = description;
        DurationMilliseconds = durationMilliseconds;
    }

    /// <summary>
    /// Gets the status reported by the health check.
    /// </summary>
    public HealthStatus Status { get; }

    /// <summary>
    /// Gets the optional, non-sensitive diagnostic description of the result.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the health-check execution duration in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; }
}
