namespace Workbench.Gateway.Logging;

using Workbench.Contracts.Scenarios;

/// <summary>
/// Defines source-generated error log messages for scenario execution.
/// </summary>
internal static partial class LogError {
    /// <summary>
    /// Defines the log level used by all messages in this class.
    /// </summary>
    private const LogLevel Level = LogLevel.Error;

    /// <summary>
    /// Defines the base event identifier for gateway error events.
    /// </summary>
    private const int EventIdBase = (int)Level * 100;

    /// <summary>
    /// Logs that scenario execution failed unexpectedly.
    /// </summary>
    /// <param name="logger">The logger used to write the event.</param>
    /// <param name="exception">
    /// The exception that caused scenario execution to fail.
    /// </param>
    /// <param name="scenarioKind">The scenario that failed.</param>
    /// <param name="architecture">
    /// The requested architecture selection, or <see langword="null"/> when no
    /// selection was available.
    /// </param>
    [LoggerMessage(
        EventId = EventIdBase + 1,
        Level = Level,
        Message = "Failed to execute scenario {ScenarioKind} for architecture {Architecture}.")]
    public static partial void ScenarioExecutionFailed(
        this ILogger logger,
        Exception exception,
        ScenarioKind scenarioKind,
        string? architecture);
}
