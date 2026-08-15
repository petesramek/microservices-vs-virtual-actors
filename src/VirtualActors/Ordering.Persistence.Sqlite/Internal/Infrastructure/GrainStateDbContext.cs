namespace Ordering.Persistence.Sqlite.Internal.Infrastructure;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides access to persisted Orleans grain states.
/// </summary>
internal sealed class GrainStateDbContext(
    DbContextOptions<GrainStateDbContext> options)
    : DbContext(options) {
    /// <summary>
    /// Gets the persisted Orleans grain states.
    /// </summary>
    public DbSet<GrainStateEntity> GrainStates =>
        Set<GrainStateEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(
            new GrainStateEntityConfiguration());
    }
}
