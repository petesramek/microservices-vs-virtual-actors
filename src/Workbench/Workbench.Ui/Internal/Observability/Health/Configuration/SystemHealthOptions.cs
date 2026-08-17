namespace Workbench.Ui.Internal.Observability.Health.Configuration;

/// <summary>
/// Defines the service endpoints used to collect system availability and health
/// observations.
/// </summary>
internal sealed class SystemHealthOptions {
    /// <summary>
    /// Identifies the configuration section containing system-health settings.
    /// </summary>
    public const string SectionName = "Observability";

    /// <summary>
    /// Gets or sets alive endpoints indexed by topology node identifier.
    /// </summary>
    /// <value>The configured service-liveness endpoint lookup.</value>
    public Dictionary<string, string> AliveEndpoints { get; set; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets health endpoints indexed by topology node identifier.
    /// </summary>
    /// <value>The configured service-readiness endpoint lookup.</value>
    public Dictionary<string, string> HealthEndpoints { get; set; } =
        new(StringComparer.Ordinal);
}
