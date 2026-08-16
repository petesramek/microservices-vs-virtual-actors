namespace Ordering.Persistence.Sqlite.Internal.Infrastructure;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides Entity Framework Core access to persisted Orleans grain states.
/// </summary>
/// <param name="options">
/// The options that configure the database context and its SQLite connection.
/// </param>
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
