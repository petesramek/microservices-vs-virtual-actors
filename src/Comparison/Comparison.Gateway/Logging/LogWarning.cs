namespace Comparison.Gateway.Logging;

internal static partial class LogWarning {
    const LogLevel Level = LogLevel.Warning;
    const int EventIdBase = (int)Level * 100;

    /// <summary>
    /// Logs that an unsupported architecture was requested.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="architecture">The requested architecture.</param>
    [LoggerMessage(
        EventId = EventIdBase + 1,
        Level = Level,
        Message = "Unsupported architecture {Architecture} was requested.")]
    public static partial void UnsupportedArchitectureRequested(
        this ILogger logger,
        string? architecture);
}
