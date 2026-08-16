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
    /// Collects only traces associated with workbench scenario runs.
    /// </summary>
    ScenarioOnly,
}