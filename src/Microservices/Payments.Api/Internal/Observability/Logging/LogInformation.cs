namespace Payments.Api.Internal.Observability.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// Defines source-generated informational log messages for payment operations.
/// </summary>
internal static partial class LogInformation {
    /// <summary>
    /// Defines the log level shared by the messages in this class.
    /// </summary>
    private const LogLevel Level = LogLevel.Information;

    /// <summary>
    /// Defines the first event ID reserved for payment informational events.
    /// </summary>
    private const int EventIdBase = (int)Level * 100;

    /// <summary>
    /// Logs that a request is being handled with its correlation identifier.
    /// </summary>
    /// <param name="logger">The logger used to write the event.</param>
    /// <param name="correlationId">
    /// The correlation identifier, or <see langword="null"/> when none was
    /// supplied.
    /// </param>
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
    /// <param name="logger">The logger used to write the event.</param>
    /// <param name="paymentId">The payment identifier.</param>
    /// <param name="orderId">The associated order identifier.</param>
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
    /// <param name="logger">The logger used to write the event.</param>
    /// <param name="paymentId">The payment identifier.</param>
    /// <param name="orderId">The associated order identifier.</param>
    /// <param name="authorized">
    /// <see langword="true"/> when the payment was authorized; otherwise
    /// <see langword="false"/>.
    /// </param>
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
