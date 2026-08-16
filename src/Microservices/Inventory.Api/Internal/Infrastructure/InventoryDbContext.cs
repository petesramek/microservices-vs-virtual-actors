namespace Inventory.Api.Internal.Infrastructure;

using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides Entity Framework Core access to inventory items and reservations.
/// </summary>
/// <param name="options">
/// The options used to configure the inventory database context.
/// </param>
public sealed class InventoryDbContext(
    DbContextOptions<InventoryDbContext> options)
    : DbContext(options) {
    /// <summary>
    /// Gets the inventory items tracked by the context.
    /// </summary>
    /// <value>The inventory-item entity set.</value>
    public DbSet<InventoryItem> Items => Set<InventoryItem>();

    /// <summary>
    /// Gets the inventory reservations tracked by the context.
    /// </summary>
    /// <value>The inventory-reservation entity set.</value>
    public DbSet<InventoryReservation> Reservations =>
        Set<InventoryReservation>();

    /// <summary>
    /// Applies the inventory entity mappings to the database model.
    /// </summary>
    /// <param name="modelBuilder">
    /// The builder used to construct the Entity Framework Core model.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="modelBuilder"/> is <see langword="null"/>.
    /// </exception>
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(
            new InventoryItemEntityConfiguration());
        modelBuilder.ApplyConfiguration(
            new InventoryReservationEntityConfiguration());
    }
}
