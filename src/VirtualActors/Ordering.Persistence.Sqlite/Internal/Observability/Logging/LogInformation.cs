namespace Ordering.Persistence.Sqlite.Internal.Observability.Logging;

using Microsoft.Extensions.Logging;

internal static partial class LogInformation {
    private const LogLevel Level = LogLevel.Information;
    private const int EventIdBase = (int)Level * 100;

    /// <summary>
    /// Logs that a SQLite grain storage provider has been initialized for a service.
    /// </summary>
    /// <param name="logger">The logger used to write the event.</param>
    /// <param name="storageName">The name of the initialized grain storage provider.</param>
    /// <param name="serviceId">The identifier of the service for which the provider was initialized.</param>
    [LoggerMessage(
        EventId = EventIdBase + 1,
        Level = Level,
        Message = "SQLite grain storage provider {StorageName} initialized for service {ServiceId}.")]
    public static partial void StorageProviderInitializedForService(
        this ILogger logger,
        string storageName,
        string serviceId);
}
