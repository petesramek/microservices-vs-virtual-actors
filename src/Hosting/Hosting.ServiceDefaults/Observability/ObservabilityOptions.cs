namespace Hosting.ServiceDefaults.Observability;

/// <summary>
/// Defines shared observability configuration.
/// </summary>
public sealed class ObservabilityOptions {
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Observability";

    /// <summary>
    /// Gets or sets the trace collection mode.
    /// </summary>
    public TraceCollectionMode TraceMode { get; set; } = TraceCollectionMode.Full;

    public TraceSource TraceSources { get; set; } = TraceSource.All;

    public MetricSource MetricSources { get; set; } = MetricSource.All;
}
