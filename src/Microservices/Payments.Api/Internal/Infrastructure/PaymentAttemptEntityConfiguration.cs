namespace Payments.Api.Internal.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payments.Api.Models;

/// <summary>
/// Configures the Entity Framework Core mapping for
/// <see cref="PaymentAttempt"/>.
/// </summary>
/// <remarks>
/// The mapping defines the payment identifier as the primary key and enforces
/// uniqueness for payment idempotency keys.
/// </remarks>
internal sealed class PaymentAttemptEntityConfiguration
    : IEntityTypeConfiguration<PaymentAttempt> {
    /// <summary>
    /// Defines the maximum length of customer identifiers and payment failure
    /// reasons.
    /// </summary>
    private const int ShortTextMaximumLength = 100;

    /// <summary>
    /// Defines the maximum length of payment idempotency keys.
    /// </summary>
    private const int IdempotencyKeyMaximumLength = 200;

    /// <summary>
    /// Configures the payment-attempt primary key, property length constraints,
    /// and unique idempotency-key index.
    /// </summary>
    /// <param name="builder">
    /// The entity-type builder used to configure the payment-attempt mapping.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder) {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(static payment => payment.PaymentId);

        builder
            .Property(static payment => payment.CustomerId)
            .HasMaxLength(ShortTextMaximumLength);

        builder
            .Property(static payment => payment.IdempotencyKey)
            .HasMaxLength(IdempotencyKeyMaximumLength);

        builder
            .Property(static payment => payment.Reason)
            .HasMaxLength(ShortTextMaximumLength);

        builder
            .HasIndex(static payment => payment.IdempotencyKey)
            .IsUnique();
    }
}
