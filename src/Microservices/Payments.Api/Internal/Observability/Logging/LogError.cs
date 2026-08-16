namespace Payments.Api.Internal.Observability.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// Defines source-generated error log messages for payment operations.
/// </summary>
internal static partial class LogError {
    /// <summary>
    /// Defines the log level shared by the messages in this class.
    /// </summary>
    private const LogLevel Level = LogLevel.Error;

    /// <summary>
    /// Defines the first event ID reserved for payment error events.
    /// </summary>
    private const int EventIdBase = (int)Level * 100;

    /// <summary>
    /// Logs that payment authorization failed unexpectedly.
    /// </summary>
    /// <param name="logger">The logger used to write the event.</param>
    /// <param name="exception">
    /// The exception that caused payment authorization to fail.
    /// </param>
    /// <param name="paymentId">The payment identifier.</param>
    /// <param name="orderId">The associated order identifier.</param>
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
