namespace Orders.Api.Data;

using Microsoft.EntityFrameworkCore;
using Orders.Api.Models;

/// <summary>
/// Orders service database context.
/// </summary>
/// <param name="options">The context options.</param>
public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options) {
    /// <summary>
    /// Gets orders.
    /// </summary>
    public DbSet<OrderRecord> Orders => Set<OrderRecord>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<OrderRecord>(entity => {
            entity.HasKey(order => order.OrderId);
            entity.Property(order => order.CustomerId).HasMaxLength(100);
            entity.Property(order => order.ProductId).HasMaxLength(100);
            entity.Property(order => order.IdempotencyKey).HasMaxLength(200);
            entity.Property(order => order.Status).HasMaxLength(50);
            entity.Property(order => order.Reason).HasMaxLength(100);
            entity.HasIndex(order => order.IdempotencyKey).IsUnique();
        });
    }
}
