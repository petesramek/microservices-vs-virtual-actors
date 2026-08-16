namespace Orders.Api.Internal.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Orders.Api.Models;

/// <summary>
/// Provides Entity Framework Core access to persisted order records.
/// </summary>
/// <param name="options">
/// The options used to configure the orders database context.
/// </param>
public sealed class OrdersDbContext(
    DbContextOptions<OrdersDbContext> options)
    : DbContext(options) {
    /// <summary>
    /// Gets the persisted order records.
    /// </summary>
    /// <value>
    /// The entity set used to query and persist <see cref="OrderRecord"/>
    /// instances.
    /// </value>
    public DbSet<OrderRecord> Orders => Set<OrderRecord>();

    /// <summary>
    /// Applies the order-record mapping to the Entity Framework Core model.
    /// </summary>
    /// <param name="modelBuilder">
    /// The builder used to construct the database model.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="modelBuilder"/> is <see langword="null"/>.
    /// </exception>
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new OrderRecordEntityConfiguration());
    }
}
