namespace Payments.Api.Logging;

internal static partial class LogError {
    const LogLevel Level = LogLevel.Error;
    const int EventIdBase = (int)Level * 100;

    /// <summary>
    /// Logs that authorizing a payment failed.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The exception that caused payment authorization to fail.</param>
    /// <param name="paymentId">The payment identifier.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="customerId">The customer identifier.</param>
    [LoggerMessage(
        EventId = EventIdBase + 1,
        Level = Level,
        Message = "Authorizing payment {PaymentId} for order {OrderId} and customer {CustomerId} failed.")]
    public static partial void PaymentAuthorizationFailed(
        this ILogger logger,
        Exception exception,
        Guid paymentId,
        Guid orderId,
        string customerId);
}
