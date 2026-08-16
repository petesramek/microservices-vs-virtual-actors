namespace Orders.Api.Internal.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Api.Models;

/// <summary>
/// Configures the Entity Framework Core mapping for <see cref="OrderRecord"/>.
/// </summary>
internal sealed class OrderRecordEntityConfiguration
    : IEntityTypeConfiguration<OrderRecord> {
    /// <summary>
    /// Defines the maximum length of customer and product identifiers.
    /// </summary>
    private const int IdentifierMaximumLength = 100;

    /// <summary>
    /// Defines the maximum length of order idempotency keys.
    /// </summary>
    private const int IdempotencyKeyMaximumLength = 200;

    /// <summary>
    /// Defines the maximum length of persisted order statuses.
    /// </summary>
    private const int StatusMaximumLength = 50;

    /// <summary>
    /// Defines the maximum length of persisted order outcome reasons.
    /// </summary>
    private const int ReasonMaximumLength = 100;

    /// <summary>
    /// Configures the order-record key, property length constraints, and unique
    /// idempotency-key index.
    /// </summary>
    /// <param name="builder">
    /// The entity-type builder used to configure the order-record mapping.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    public void Configure(EntityTypeBuilder<OrderRecord> builder) {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(static order => order.OrderId);

        builder
            .Property(static order => order.CustomerId)
            .HasMaxLength(IdentifierMaximumLength);

        builder
            .Property(static order => order.ProductId)
            .HasMaxLength(IdentifierMaximumLength);

        builder
            .Property(static order => order.IdempotencyKey)
            .HasMaxLength(IdempotencyKeyMaximumLength);

        builder
            .Property(static order => order.Status)
            .HasMaxLength(StatusMaximumLength);

        builder
            .Property(static order => order.Reason)
            .HasMaxLength(ReasonMaximumLength);

        builder
            .HasIndex(static order => order.IdempotencyKey)
            .IsUnique();
    }
}
