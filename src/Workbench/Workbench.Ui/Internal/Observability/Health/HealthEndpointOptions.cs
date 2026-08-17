namespace Workbench.Ui.Internal.Observability.Health;

/// <summary>
/// Defines the service health endpoints supplied by the AppHost.
/// </summary>
internal sealed class HealthEndpointOptions
    : Dictionary<string, string> {
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "Observability:HealthEndpoints";
}
