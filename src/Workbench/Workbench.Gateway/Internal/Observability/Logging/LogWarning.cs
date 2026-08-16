namespace Workbench.Gateway.Logging;

/// <summary>
/// Defines source-generated warning log messages for gateway requests.
/// </summary>
internal static partial class LogWarning {
    /// <summary>
    /// Defines the log level used by all messages in this class.
    /// </summary>
    private const LogLevel Level = LogLevel.Warning;

    /// <summary>
    /// Defines the base event identifier for gateway warning events.
    /// </summary>
    private const int EventIdBase = (int)Level * 100;

    /// <summary>
    /// Logs that a request specified an unsupported architecture selection.
    /// </summary>
    /// <param name="logger">The logger used to write the event.</param>
    /// <param name="architecture">
    /// The unsupported architecture value, or <see langword="null"/> when the
    /// request supplied no value.
    /// </param>
    [LoggerMessage(
        EventId = EventIdBase + 1,
        Level = Level,
        Message = "Requested unsupported architecture {Architecture}.")]
    public static partial void UnsupportedArchitectureRequested(
        this ILogger logger,
        string? architecture);
}
