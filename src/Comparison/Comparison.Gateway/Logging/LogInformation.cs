namespace Comparison.Gateway.Logging;

using Comparison.Contracts;

internal static partial class LogInformation {
    const LogLevel Level = LogLevel.Information;
    const int EventIdBase = (int)Level * 100;

    /// <summary>
    /// Logs that a request with a correlation identifier is being handled.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="correlationId">The correlation identifier.</param>
    [LoggerMessage(
        EventId = EventIdBase + 1,
        Level = Level,
        Message = "Handling request with correlation id {CorrelationId}.")]
    public static partial void HandlingRequestWithCorrelationId(
        this ILogger logger,
        string? correlationId);

    /// <summary>
    /// Logs that a scenario is being run.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="scenarioKind">The scenario kind.</param>
    /// <param name="architecture">The selected architecture.</param>
    [LoggerMessage(
        EventId = EventIdBase + 2,
        Level = Level,
        Message = "Running scenario {ScenarioKind} for architecture selection {Architecture}.")]
    public static partial void RunningScenario(
        this ILogger logger,
        ScenarioKind scenarioKind,
        string? architecture);

    /// <summary>
    /// Logs that a scenario completed.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="scenarioKind">The scenario kind.</param>
    /// <param name="architecture">The selected architecture.</param>
    /// <param name="microservicesCompleted">Whether the Microservices architecture completed.</param>
    /// <param name="virtualActorsCompleted">Whether the Virtual Actors architecture completed.</param>
    [LoggerMessage(
        EventId = EventIdBase + 3,
        Level = Level,
        Message = "Scenario {ScenarioKind} completed for architecture selection {Architecture} with microservices completed {MicroservicesCompleted} and virtual actors completed {VirtualActorsCompleted}.")]
    public static partial void ScenarioCompleted(
        this ILogger logger,
        ScenarioKind scenarioKind,
        string? architecture,
        bool microservicesCompleted,
        bool virtualActorsCompleted);
}
