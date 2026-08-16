namespace Hosting.ServiceDefaults.Observability;

using System.Diagnostics;

/// <summary>
/// Defines the shared names and objects used to emit scenario traces and
/// workflow metrics.
/// </summary>
/// <remarks>
/// The constants in this type are telemetry contracts. Keep names stable to
/// preserve queries, dashboards, alerts, and cross-service trace correlation.
/// </remarks>
public static class ScenarioInstrumentation {
    /// <summary>
    /// Identifies the instrumentation library that emits scenario traces and
    /// workflow metrics.
    /// </summary>
    public const string Name = "Scenario.Workflows";

    /// <summary>
    /// Identifies the activity source used for scenario traces.
    /// </summary>
    public const string ActivitySourceName = Name;

    /// <summary>
    /// Identifies the meter used for workflow metrics.
    /// </summary>
    public const string MeterName = Name;

    /// <summary>
    /// Gets the process-wide activity source used to create scenario traces.
    /// </summary>
    /// <value>The shared scenario activity source.</value>
    /// <remarks>
    /// The source is intentionally reused for the lifetime of the process.
    /// Consumers must register <see cref="ActivitySourceName"/> with their trace
    /// provider before activities can be recorded.
    /// </remarks>
    public static ActivitySource ActivitySource { get; } =
        new(ActivitySourceName);

    /// <summary>
    /// Creates the display name for a scenario activity.
    /// </summary>
    /// <param name="scenario">
    /// The non-empty scenario name included in the activity name.
    /// </param>
    /// <returns>
    /// An activity name in the form <c>Scenario: {scenario}</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="scenario"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="scenario"/> is empty or consists only of white-space
    /// characters.
    /// </exception>
    public static string GetActivityName(string scenario) {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);

        return $"Scenario: {scenario}";
    }

    /// <summary>
    /// Contains the HTTP header contract used to identify scenario traffic.
    /// </summary>
    public static class Headers {
        /// <summary>
        /// Identifies the HTTP header that marks scenario-related requests.
        /// </summary>
        public const string ScenarioRun = "X-Scenario-Run";

        /// <summary>
        /// Identifies the header value that marks a request as scenario-related.
        /// </summary>
        public const string ScenarioRunValue = "true";
    }

    /// <summary>
    /// Contains stable names for workflow metric instruments.
    /// </summary>
    public static class InstrumentNames {
        /// <summary>
        /// Identifies the counter that records terminal workflow runs.
        /// </summary>
        public const string RunCount = "workflow.run.count";

        /// <summary>
        /// Identifies the histogram that records terminal workflow duration.
        /// </summary>
        public const string RunDuration = "workflow.run.duration";
    }

    /// <summary>
    /// Contains stable activity and metric tag names for scenario telemetry.
    /// </summary>
    /// <remarks>
    /// Values assigned to metric tags should remain bounded to avoid unbounded
    /// metric cardinality.
    /// </remarks>
    public static class TagNames {
        /// <summary>
        /// Identifies the tag containing the selected architecture.
        /// </summary>
        public const string Architecture = "scenario.architecture";

        /// <summary>
        /// Identifies the tag containing the requested concurrency level.
        /// </summary>
        public const string ConcurrentRequests = "scenario.concurrent_requests";

        /// <summary>
        /// Identifies the tag containing the scenario product identifier.
        /// </summary>
        public const string ProductId = "scenario.product.id";

        /// <summary>
        /// Identifies the tag containing the selected scenario kind.
        /// </summary>
        public const string ScenarioKind = "scenario.kind";

        /// <summary>
        /// Identifies the tag indicating that an activity represents a scenario
        /// run.
        /// </summary>
        public const string ScenarioRun = "scenario.run";
    }
}
