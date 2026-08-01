namespace Comparison.Gateway.Logging;

using Comparison.Contracts;

internal static partial class LogInformation {
    const LogLevel Level = LogLevel.Information;

    [LoggerMessage(
        Level = Level,
        Message = "Handling request with correlation id {CorrelationId}.")]
    public static partial void HandlingRequestWithCorrelationId(this ILogger logger, string? correlationId);

    [LoggerMessage(
        Level = Level,
        Message = "Running scenario {ScenarioKind} for architecture selection {Architecture}.")]
    public static partial void RunningScenarioForArchitecture(this ILogger logger, ScenarioKind scenarioKind, string? architecture);
}
