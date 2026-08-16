namespace Inventory.Api.Internal.Infrastructure;

using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configures the Entity Framework Core mapping for
/// <see cref="InventoryItem"/>.
/// </summary>
internal sealed class InventoryItemEntityConfiguration
    : IEntityTypeConfiguration<InventoryItem> {
    /// <summary>
    /// Defines the maximum length of inventory product identifiers.
    /// </summary>
    private const int ProductIdentifierMaximumLength = 100;

    /// <summary>
    /// Configures the inventory-item primary key and property constraints.
    /// </summary>
    /// <param name="builder">
    /// The entity-type builder used to configure the inventory-item mapping.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    public void Configure(EntityTypeBuilder<InventoryItem> builder) {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(static item => item.ProductId);

        builder
            .Property(static item => item.ProductId)
            .HasMaxLength(ProductIdentifierMaximumLength);
    }
}
