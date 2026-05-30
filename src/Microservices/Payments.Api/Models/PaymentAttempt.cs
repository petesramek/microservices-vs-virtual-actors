namespace Payments.Api.Models;

/// <summary>
/// Represents a payment authorization attempt.
/// </summary>
public sealed class PaymentAttempt
{
    /// <summary>
    /// Gets or sets the payment identifier.
    /// </summary>
    public Guid PaymentId { get; set; }

    /// <summary>
    /// Gets or sets the order identifier.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Gets or sets the customer identifier.
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether payment was authorized.
    /// </summary>
    public bool Authorized { get; set; }

    /// <summary>
    /// Gets or sets the failure reason.
    /// </summary>
    public string? Reason { get; set; }
}
