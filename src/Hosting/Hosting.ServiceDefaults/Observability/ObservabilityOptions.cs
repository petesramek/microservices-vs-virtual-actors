namespace Hosting.ServiceDefaults.Observability;

/// <summary>
/// Defines the supported trace collection modes.
/// </summary>
public enum TraceCollectionMode {
    /// <summary>
    /// Collects all configured application and infrastructure traces.
    /// </summary>
    Full,

    /// <summary>
    /// Collects only traces associated with comparison scenario runs.
    /// </summary>
    ScenarioOnly,
}

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
    public TraceCollectionMode TraceMode { get; set; } =
        TraceCollectionMode.Full;
}
