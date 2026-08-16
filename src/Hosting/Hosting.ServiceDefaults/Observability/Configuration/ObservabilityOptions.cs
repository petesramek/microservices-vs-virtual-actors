namespace Hosting.ServiceDefaults.Observability.Configuration;
/// <summary>
/// Defines the shared configuration that controls OpenTelemetry trace and
/// metric collection for application services.
/// </summary>
/// <remarks>
/// Values are bound from the configuration section identified by
/// <see cref="SectionName"/>. Trace and metric source properties are flags and
/// may contain multiple supported values.
/// </remarks>
public sealed class ObservabilityOptions {
    /// <summary>
    /// Identifies the configuration section bound to these options.
    /// </summary>
    public const string SectionName = "Observability";

    /// <summary>
    /// Gets or sets the trace collection mode.
    /// </summary>
    /// <value>
    /// The mode that determines which requests are eligible for trace
    /// collection. The default is <see cref="TraceCollectionMode.Full"/>.
    /// </value>
    public TraceCollectionMode TraceMode { get; set; } = TraceCollectionMode.Full;

    /// <summary>
    /// Gets or sets the trace sources enabled for OpenTelemetry collection.
    /// </summary>
    /// <value>
    /// A bitwise combination of <see cref="TraceSource"/> values. The default
    /// enables <see cref="TraceSource.All"/> supported trace sources.
    /// </value>
    public TraceSource TraceSources { get; set; } = TraceSource.All;

    /// <summary>
    /// Gets or sets the metric sources enabled for OpenTelemetry collection.
    /// </summary>
    /// <value>
    /// A bitwise combination of <see cref="MetricSource"/> values. The default
    /// enables <see cref="MetricSource.All"/> supported metric sources.
    /// </value>
    public MetricSource MetricSources { get; set; } = MetricSource.All;
}
