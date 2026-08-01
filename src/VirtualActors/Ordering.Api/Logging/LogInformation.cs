namespace Ordering.Api.Logging;

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
    public static partial void HandlingRequestWithCorrelationId(this ILogger logger, string? correlationId);

    /// <summary>
    /// Logs that an order completed with a status.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="status">The resulting order status.</param>
    [LoggerMessage(
        EventId = EventIdBase + 2,
        Level = Level,
        Message = "Order {OrderId} completed with status {Status}.")]
    public static partial void OrderCompletedWithStatus(this ILogger logger, Guid orderId, string status);

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
    /// Logs that an order is being placed.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="customerId">The customer identifier.</param>
    /// <param name="productId">The product identifier.</param>
    /// <param name="quantity">The requested quantity.</param>
    [LoggerMessage(
        EventId = EventIdBase + 5,
        Level = Level,
        Message = "Placing order {OrderId} for customer {CustomerId}, product {ProductId}, and quantity {Quantity}.")]
    public static partial void PlacingOrder(
        this ILogger logger,
        Guid orderId,
        string customerId,
        string productId,
        int quantity);

    /// <summary>
    /// Logs that an order was retrieved with its current status.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="status">The current order status.</param>
    [LoggerMessage(
        EventId = EventIdBase + 6,
        Level = Level,
        Message = "Order {OrderId} was retrieved with status {Status}.")]
    public static partial void OrderRetrievedWithStatus(
        this ILogger logger,
        Guid orderId,
        string status);

    /// <summary>
    /// Logs that inventory for a product is being reset.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="productId">The product identifier.</param>
    /// <param name="quantity">The requested quantity.</param>
    [LoggerMessage(
        EventId = EventIdBase + 7,
        Level = Level,
        Message = "Resetting inventory for product {ProductId} to {Quantity}.")]
    public static partial void ResettingInventory(
        this ILogger logger,
        string productId,
        int quantity);
}
