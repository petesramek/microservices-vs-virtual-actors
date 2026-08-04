namespace Inventory.Api.Logging;

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
    /// Logs that inventory for a product is being reset.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="productId">The product identifier.</param>
    /// <param name="quantity">The requested quantity.</param>
    [LoggerMessage(
        EventId = EventIdBase + 2,
        Level = Level,
        Message = "Resetting inventory for product {ProductId} to {Quantity}.")]
    public static partial void ResettingInventory(
        this ILogger logger,
        string productId,
        int quantity);

    /// <summary>
    /// Logs that inventory for a product was reset.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="productId">The product identifier.</param>
    /// <param name="availableQuantity">The available quantity after the reset.</param>
    [LoggerMessage(
        EventId = EventIdBase + 3,
        Level = Level,
        Message = "Inventory for product {ProductId} was reset to {AvailableQuantity}.")]
    public static partial void InventoryReset(
        this ILogger logger,
        string productId,
        int availableQuantity);

    /// <summary>
    /// Logs that inventory for a product was retrieved.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="productId">The product identifier.</param>
    /// <param name="availableQuantity">The available quantity.</param>
    [LoggerMessage(
        EventId = EventIdBase + 4,
        Level = Level,
        Message = "Inventory for product {ProductId} was retrieved with {AvailableQuantity} available.")]
    public static partial void InventoryRetrieved(
        this ILogger logger,
        string productId,
        int availableQuantity);

    /// <summary>
    /// Logs that inventory is being reserved for an order.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="productId">The product identifier.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="reservationId">The reservation identifier.</param>
    /// <param name="quantity">The requested quantity.</param>
    [LoggerMessage(
        EventId = EventIdBase + 5,
        Level = Level,
        Message = "Reserving {Quantity} item(s) of product {ProductId} for order {OrderId} with reservation {ReservationId}.")]
    public static partial void ReservingInventory(
        this ILogger logger,
        string productId,
        Guid orderId,
        Guid reservationId,
        int quantity);

    /// <summary>
    /// Logs that an inventory reservation completed.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="productId">The product identifier.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="reservationId">The reservation identifier.</param>
    /// <param name="reserved">Whether the inventory was reserved.</param>
    /// <param name="availableQuantity">The remaining available quantity.</param>
    [LoggerMessage(
        EventId = EventIdBase + 6,
        Level = Level,
        Message = "Inventory reservation {ReservationId} for order {OrderId} and product {ProductId} completed with reserved {Reserved} and {AvailableQuantity} available.")]
    public static partial void InventoryReservationCompleted(
        this ILogger logger,
        string productId,
        Guid orderId,
        Guid reservationId,
        bool reserved,
        int availableQuantity);

    /// <summary>
    /// Logs that an inventory reservation is being released.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="productId">The product identifier.</param>
    /// <param name="reservationId">The reservation identifier.</param>
    [LoggerMessage(
        EventId = EventIdBase + 7,
        Level = Level,
        Message = "Releasing inventory reservation {ReservationId} for product {ProductId}.")]
    public static partial void ReleasingInventory(
        this ILogger logger,
        string productId,
        Guid reservationId);

    /// <summary>
    /// Logs that an inventory reservation was released.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="productId">The product identifier.</param>
    /// <param name="reservationId">The reservation identifier.</param>
    /// <param name="availableQuantity">The available quantity after the release.</param>
    [LoggerMessage(
        EventId = EventIdBase + 8,
        Level = Level,
        Message = "Inventory reservation {ReservationId} for product {ProductId} was released with {AvailableQuantity} available.")]
    public static partial void InventoryReleased(
        this ILogger logger,
        string productId,
        Guid reservationId,
        int availableQuantity);
}
