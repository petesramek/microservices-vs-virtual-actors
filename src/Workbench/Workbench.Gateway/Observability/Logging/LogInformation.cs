namespace Workbench.Gateway.Logging;

using Workbench.Contracts.Scenarios;

internal static partial class LogInformation {
    private const LogLevel Level = LogLevel.Information;
    private const int EventIdBase = (int)Level * 100;

    /// <summary>
    /// Logs that a scenario is starting.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="scenarioKind">The scenario kind.</param>
    /// <param name="architecture">The selected architecture.</param>
    [LoggerMessage(
        EventId = EventIdBase + 1,
        Level = Level,
        Message = "Starting scenario {ScenarioKind} for architecture {Architecture}.")]
    public static partial void StartingScenario(
        this ILogger logger,
        ScenarioKind scenarioKind,
        string? architecture);

    /// <summary>
    /// Logs that a scenario completed.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="scenarioKind">The scenario kind.</param>
    /// <param name="architecture">The selected architecture.</param>
    /// <param name="microservicesExecuted">Whether the Microservices architecture was executed.</param>
    /// <param name="virtualActorsExecuted">Whether the Virtual Actors architecture was executed.</param>
    [LoggerMessage(
        EventId = EventIdBase + 2,
        Level = Level,
        Message = "Completed scenario {ScenarioKind} for architecture {Architecture}. Microservices executed: {MicroservicesExecuted}; virtual actors executed: {VirtualActorsExecuted}.")]
    public static partial void ScenarioCompleted(
        this ILogger logger,
        ScenarioKind scenarioKind,
        string? architecture,
        bool microservicesExecuted,
        bool virtualActorsExecuted);
}
