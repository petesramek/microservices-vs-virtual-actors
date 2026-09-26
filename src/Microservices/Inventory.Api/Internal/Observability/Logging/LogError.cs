namespace Inventory.Api.Internal.Observability.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// Defines source-generated error log messages for inventory operations.
/// </summary>
internal static partial class LogError {
    /// <summary>
    /// Defines the log level shared by the messages in this class.
    /// </summary>
    private const LogLevel Level = LogLevel.Error;

    /// <summary>
    /// Defines the first event ID reserved for inventory error events.
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
    /// Logs that retrieving inventory for a product failed.
    /// </summary>
    /// <param name="logger">The logger used to write the event.</param>
    /// <param name="exception">
    /// The exception that caused inventory retrieval to fail.
    /// </param>
    /// <param name="productId">The product identifier.</param>
    [LoggerMessage(
        EventId = EventIdBase + 2,
        Level = Level,
        Message = "Retrieving inventory for product {ProductId} failed.")]
    public static partial void InventoryRetrievalFailed(
        this ILogger logger,
        Exception exception,
        string productId);

    /// <summary>
    /// Logs that reserving inventory for an order failed.
    /// </summary>
    /// <param name="logger">The logger used to write the event.</param>
    /// <param name="exception">
    /// The exception that caused inventory reservation to fail.
    /// </param>
    /// <param name="productId">The product identifier.</param>
    /// <param name="orderId">The associated order identifier.</param>
    /// <param name="reservationId">The reservation identifier.</param>
    /// <param name="quantity">The requested inventory quantity.</param>
    [LoggerMessage(
        EventId = EventIdBase + 3,
        Level = Level,
        Message = "Reserving {Quantity} item(s) of product {ProductId} for order {OrderId} with reservation {ReservationId} failed.")]
    public static partial void InventoryReservationFailed(
        this ILogger logger,
        Exception exception,
        string productId,
        Guid orderId,
        Guid reservationId,
        int quantity);

    /// <summary>
    /// Logs that releasing an inventory reservation failed.
    /// </summary>
    /// <param name="logger">The logger used to write the event.</param>
    /// <param name="exception">
    /// The exception that caused the inventory release to fail.
    /// </param>
    /// <param name="productId">The product identifier.</param>
    /// <param name="reservationId">The reservation identifier.</param>
    [LoggerMessage(
        EventId = EventIdBase + 4,
        Level = Level,
        Message = "Releasing inventory reservation {ReservationId} for product {ProductId} failed.")]
    public static partial void InventoryReleaseFailed(
        this ILogger logger,
        Exception exception,
        string productId,
        Guid reservationId);
}
