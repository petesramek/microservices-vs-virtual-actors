namespace Ordering.Api.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// Defines source-generated error log messages for ordering API operations.
/// </summary>
internal static partial class LogError {
    /// <summary>
    /// Defines the log level shared by the messages in this class.
    /// </summary>
    private const LogLevel Level = LogLevel.Error;

    /// <summary>
    /// Defines the first event ID reserved for ordering API error events.
    /// </summary>
    private const int EventIdBase = (int)Level * 100;

    /// <summary>
    /// Logs that resetting inventory for a product failed.
    /// </summary>
    /// <param name="logger">The logger used to write the event.</param>
    /// <param name="exception">
    /// The exception that caused the inventory reset to fail.
    /// </param>
    /// <param name="productId">The product identifier.</param>
    /// <param name="quantity">The requested inventory quantity.</param>
    [LoggerMessage(
        EventId = EventIdBase + 1,
        Level = Level,
        Message = "Resetting inventory for product {ProductId} to {Quantity} failed.")]
    public static partial void InventoryResetFailed(
        this ILogger logger,
        Exception exception,
        string productId,
        int quantity);

    /// <summary>
    /// Logs that placing an order failed.
    /// </summary>
    /// <param name="logger">The logger used to write the event.</param>
    /// <param name="exception">
    /// The exception that caused order placement to fail.
    /// </param>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="productId">The product identifier.</param>
    /// <param name="quantity">The requested product quantity.</param>
    [LoggerMessage(
        EventId = EventIdBase + 2,
        Level = Level,
        Message = "Placing order {OrderId} for product {ProductId} and quantity {Quantity} failed.")]
    public static partial void OrderPlacementFailed(
        this ILogger logger,
        Exception exception,
        Guid orderId,
        string productId,
        int quantity);

    /// <summary>
    /// Logs that retrieving inventory for a product failed.
    /// </summary>
    /// <param name="logger">The logger used to write the event.</param>
    /// <param name="exception">
    /// The exception that caused inventory retrieval to fail.
    /// </param>
    /// <param name="productId">The product identifier.</param>
    [LoggerMessage(
        EventId = EventIdBase + 3,
        Level = Level,
        Message = "Retrieving inventory for product {ProductId} failed.")]
    public static partial void InventoryRetrievalFailed(
        this ILogger logger,
        Exception exception,
        string productId);

    /// <summary>
    /// Logs that retrieving an order failed.
    /// </summary>
    /// <param name="logger">The logger used to write the event.</param>
    /// <param name="exception">
    /// The exception that caused order retrieval to fail.
    /// </param>
    /// <param name="orderId">The order identifier.</param>
    [LoggerMessage(
        EventId = EventIdBase + 4,
        Level = Level,
        Message = "Retrieving order {OrderId} failed.")]
    public static partial void OrderRetrievalFailed(
        this ILogger logger,
        Exception exception,
        Guid orderId);
}
