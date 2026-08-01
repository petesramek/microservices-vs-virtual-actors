namespace Ordering.Api.Logging;

internal static partial class LogError {
    const LogLevel Level = LogLevel.Error;
    const int EventIdBase = (int)Level * 100;
}
