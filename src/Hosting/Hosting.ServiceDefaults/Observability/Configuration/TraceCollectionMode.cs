namespace Hosting.ServiceDefaults.Observability.Configuration;
/// <summary>
/// Specifies how configured trace sources are collected.
/// </summary>
/// <remarks>
/// The collection mode controls filtering and sampling. The enabled
/// instrumentation is selected separately through <see cref="TraceSource"/>.
/// </remarks>
public enum TraceCollectionMode {
    /// <summary>
    /// Collects non-health traces from all configured trace sources.
    /// </summary>
    Full,

    /// <summary>
    /// Collects traces rooted in workbench scenario runs and preserves their
    /// distributed descendants through parent-based sampling.
    /// </summary>
    ScenarioOnly,
}
