namespace Inventory.Api.Internal.Infrastructure;

using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configures the Entity Framework Core mapping for
/// <see cref="InventoryReservation"/>.
/// </summary>
internal sealed class InventoryReservationEntityConfiguration
    : IEntityTypeConfiguration<InventoryReservation> {
    /// <summary>
    /// Defines the maximum length of inventory product identifiers.
    /// </summary>
    private const int ProductIdentifierMaximumLength = 100;

    /// <summary>
    /// Configures the reservation primary key, product-identifier constraint,
    /// and order lookup index.
    /// </summary>
    /// <param name="builder">
    /// The entity-type builder used to configure the reservation mapping.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    public void Configure(
        EntityTypeBuilder<InventoryReservation> builder) {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(static reservation => reservation.ReservationId);

        builder
            .Property(static reservation => reservation.ProductId)
            .HasMaxLength(ProductIdentifierMaximumLength);

        builder.HasIndex(static reservation => reservation.OrderId);
    }
}
