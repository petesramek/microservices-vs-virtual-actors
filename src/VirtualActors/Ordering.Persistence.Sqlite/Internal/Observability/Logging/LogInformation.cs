namespace Ordering.Persistence.Sqlite.Internal.Observability.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// Defines source-generated informational log messages for SQLite grain-state
/// persistence.
/// </summary>
internal static partial class LogInformation {
    /// <summary>
    /// Defines the log level shared by the messages in this class.
    /// </summary>
    private const LogLevel Level = LogLevel.Information;

    /// <summary>
    /// Defines the first event ID reserved for informational persistence events.
    /// </summary>
    private const int EventIdBase = (int)Level * 100;

    /// <summary>
    /// Logs that a SQLite grain storage provider has been initialized for an
    /// Orleans service.
    /// </summary>
    /// <param name="logger">The logger used to write the event.</param>
    /// <param name="storageName">
    /// The registered name of the initialized grain storage provider.
    /// </param>
    /// <param name="serviceId">
    /// The identifier of the Orleans service for which the provider was
    /// initialized.
    /// </param>
    [LoggerMessage(
        EventId = EventIdBase + 1,
        Level = Level,
        Message = "SQLite grain storage provider {StorageName} initialized for service {ServiceId}.")]
    public static partial void StorageProviderInitializedForService(
        this ILogger logger,
        string storageName,
        string serviceId);
}
