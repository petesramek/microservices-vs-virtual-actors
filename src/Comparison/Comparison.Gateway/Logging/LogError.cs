namespace Comparison.Gateway.Logging;

using Comparison.Contracts;

internal static partial class LogError {
    const LogLevel Level = LogLevel.Error;
    const int EventIdBase = (int)Level * 100;

    /// <summary>
    /// Logs that scenario execution failed.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The exception that caused scenario execution to fail.</param>
    /// <param name="scenarioKind">The scenario kind.</param>
    /// <param name="architecture">The selected architecture.</param>
    [LoggerMessage(
        EventId = EventIdBase + 1,
        Level = Level,
        Message = "Scenario {ScenarioKind} execution failed for architecture selection {Architecture}.")]
    public static partial void ScenarioExecutionFailed(
        this ILogger logger,
        Exception exception,
        ScenarioKind scenarioKind,
        string? architecture);
}
