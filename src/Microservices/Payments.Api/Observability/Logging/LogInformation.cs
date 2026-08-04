namespace Payments.Api.Logging;

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
    /// Logs that payment authorization is starting.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="paymentId">The payment identifier.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="customerId">The customer identifier.</param>
    [LoggerMessage(
        EventId = EventIdBase + 2,
        Level = Level,
        Message = "Authorizing payment {PaymentId} for order {OrderId} and customer {CustomerId}.")]
    public static partial void AuthorizingPayment(
        this ILogger logger,
        Guid paymentId,
        Guid orderId,
        string customerId);

    /// <summary>
    /// Logs that payment authorization completed.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="paymentId">The payment identifier.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="authorized">Whether the payment was authorized.</param>
    [LoggerMessage(
        EventId = EventIdBase + 3,
        Level = Level,
        Message = "Payment authorization {PaymentId} for order {OrderId} completed with authorized {Authorized}.")]
    public static partial void PaymentAuthorizationCompleted(
        this ILogger logger,
        Guid paymentId,
        Guid orderId,
        bool authorized);
}
