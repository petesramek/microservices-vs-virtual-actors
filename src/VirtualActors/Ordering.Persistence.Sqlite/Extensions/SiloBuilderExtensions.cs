namespace Ordering.Persistence.Sqlite.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Persistence.Sqlite.Storage;
using Orleans.Hosting;
using Orleans.Runtime.Hosting;

/// <summary>
/// Provides registration methods for SQLite-backed Orleans grain storage.
/// </summary>
public static class SiloBuilderExtensions {
    /// <summary>
    /// Adds a named SQLite-backed Orleans grain storage provider.
    /// </summary>
    /// <param name="siloBuilder">The Orleans silo builder.</param>
    /// <param name="storageProviderName">
    /// The storage provider name used by persistent grain state.
    /// </param>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <returns>The Orleans silo builder.</returns>
    public static ISiloBuilder AddSqliteGrainStorage(
        this ISiloBuilder siloBuilder,
        string storageProviderName,
        string connectionString) {
        ArgumentNullException.ThrowIfNull(siloBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageProviderName);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        siloBuilder.Services.AddPooledDbContextFactory<GrainStateDbContext>(
            options => options.UseSqlite(connectionString));

        siloBuilder.Services.AddGrainStorage<SqliteGrainStorage>(
            storageProviderName,
            static (serviceProvider, name) =>
                ActivatorUtilities.CreateInstance<SqliteGrainStorage>(
                    serviceProvider,
                    name));

        return siloBuilder;
    }
}
