using Microsoft.EntityFrameworkCore;
using Inventory.Api.Models;

namespace Inventory.Api.Data;

/// <summary>
/// Inventory service database context.
/// </summary>
/// <param name="options">The context options.</param>
public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets inventory items.
    /// </summary>
    public DbSet<InventoryItem> Items => Set<InventoryItem>();

    /// <summary>
    /// Gets inventory reservations.
    /// </summary>
    public DbSet<InventoryReservation> Reservations => Set<InventoryReservation>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(item => item.ProductId);
            entity.Property(item => item.ProductId).HasMaxLength(100);
        });

        modelBuilder.Entity<InventoryReservation>(entity =>
        {
            entity.HasKey(reservation => reservation.ReservationId);
            entity.Property(reservation => reservation.ProductId).HasMaxLength(100);
            entity.HasIndex(reservation => reservation.OrderId);
        });
    }
}
