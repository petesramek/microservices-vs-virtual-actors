namespace Workbench.Gateway.Logging;

using Workbench.Contracts.Scenarios;

/// <summary>
/// Defines source-generated informational log messages for scenario execution.
/// </summary>
internal static partial class LogInformation {
    /// <summary>
    /// Defines the log level used by all messages in this class.
    /// </summary>
    private const LogLevel Level = LogLevel.Information;

    /// <summary>
    /// Defines the base event identifier for gateway informational events.
    /// </summary>
    private const int EventIdBase = (int)Level * 100;

    /// <summary>
    /// Logs that scenario execution is starting.
    /// </summary>
    /// <param name="logger">The logger used to write the event.</param>
    /// <param name="scenarioKind">The scenario being executed.</param>
    /// <param name="architecture">
    /// The requested architecture selection, or <see langword="null"/> when no
    /// selection was available.
    /// </param>
    [LoggerMessage(
        EventId = EventIdBase + 1,
        Level = Level,
        Message = "Starting scenario {ScenarioKind} for architecture {Architecture}.")]
    public static partial void StartingScenario(
        this ILogger logger,
        string architecture,
        ScenarioKind scenarioKind);

    /// <summary>
    /// Logs that scenario execution completed.
    /// </summary>
    /// <param name="logger">The logger used to write the event.</param>
    /// <param name="scenarioKind">The scenario that completed.</param>
    /// <param name="architecture">
    /// The requested architecture selection, or <see langword="null"/> when no
    /// selection was available.
    /// </param>
    [LoggerMessage(
        EventId = EventIdBase + 2,
        Level = Level,
        Message = "Completed scenario {ScenarioKind} for architecture {Architecture}.")]
    public static partial void ScenarioCompleted(
        this ILogger logger,
        string architecture,
        ScenarioKind scenarioKind);
}
