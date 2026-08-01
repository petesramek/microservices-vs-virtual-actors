namespace Ordering.Api.Logging;

internal static partial class LogError {
    const LogLevel Level = LogLevel.Error;
    const int EventIdBase = (int)Level * 100;

    /// <summary>
    /// Logs that resetting inventory for a product failed.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The exception that caused the inventory reset to fail.</param>
    /// <param name="productId">The product identifier.</param>
    /// <param name="quantity">The requested quantity.</param>
    [LoggerMessage(
        EventId = EventIdBase + 1,
        Level = Level,
        Message = "Resetting inventory for product {ProductId} to {Quantity} failed.")]
    public static partial void InventoryResetFailed(
        this ILogger logger,
        Exception exception,
        string productId,
        int quantity);
}
