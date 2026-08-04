namespace Hosting.ServiceDefaults.Telemetry;

using System.Diagnostics;

/// <summary>
/// Defines shared tracing metadata for workbench scenario executions.
/// </summary>
public static class ScenarioTelemetry {
    /// <summary>
    /// The activity source name used for workbench scenario traces.
    /// </summary>
    public const string ActivitySourceName = "Workbench.Scenarios";

    /// <summary>
    /// The activity name used for a scenario execution.
    /// </summary>
    public const string RunScenarioActivityName = "Run workbench scenario";

    /// <summary>
    /// The internal HTTP header used to identify scenario-related requests.
    /// </summary>
    public const string ScenarioHeaderName = "X-Scenario-Run";

    /// <summary>
    /// The value used to mark scenario-related HTTP requests.
    /// </summary>
    public const string ScenarioHeaderValue = "true";

    /// <summary>
    /// The activity tag indicating that a trace represents a scenario run.
    /// </summary>
    public const string ScenarioRunTagName = "scenario.run";

    /// <summary>
    /// The activity tag containing the selected scenario kind.
    /// </summary>
    public const string ScenarioKindTagName = "scenario.kind";

    /// <summary>
    /// The activity tag containing the selected architecture.
    /// </summary>
    public const string ArchitectureTagName = "scenario.architecture";

    /// <summary>
    /// The activity tag containing the scenario product identifier.
    /// </summary>
    public const string ProductIdTagName = "scenario.product.id";

    /// <summary>
    /// The activity tag containing the requested concurrency level.
    /// </summary>
    public const string ConcurrentRequestsTagName =
        "scenario.concurrent_requests";

    /// <summary>
    /// Gets the activity source used to create scenario execution traces.
    /// </summary>
    public static ActivitySource ActivitySource { get; } =
        new(ActivitySourceName);
}
