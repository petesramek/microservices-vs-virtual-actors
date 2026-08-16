namespace Payments.Api.Models;

/// <summary>
/// Represents a persisted payment authorization attempt.
/// </summary>
/// <remarks>
/// The entity records the identifiers used to correlate and deduplicate an
/// authorization request together with its terminal authorization outcome.
/// </remarks>
public sealed class PaymentAttempt {
    /// <summary>
    /// Gets or sets the payment identifier.
    /// </summary>
    /// <value>The unique identifier of the payment attempt.</value>
    public Guid PaymentId { get; set; }

    /// <summary>
    /// Gets or sets the associated order identifier.
    /// </summary>
    /// <value>The identifier of the order being paid.</value>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Gets or sets the customer identifier.
    /// </summary>
    /// <value>The identifier of the customer requesting authorization.</value>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    /// <value>
    /// The key used to identify repeated payment authorization requests.
    /// </value>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the payment was authorized.
    /// </summary>
    /// <value>
    /// <see langword="true"/> when authorization succeeded; otherwise
    /// <see langword="false"/>.
    /// </value>
    public bool Authorized { get; set; }

    /// <summary>
    /// Gets or sets the authorization failure reason.
    /// </summary>
    /// <value>
    /// The optional reason authorization failed, or <see langword="null"/> when
    /// no failure reason is recorded.
    /// </value>
    public string? Reason { get; set; }
}
