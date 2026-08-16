namespace Ordering.Persistence.Sqlite.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Persistence.Sqlite.Internal.Infrastructure;
using Orleans.Hosting;
using Orleans.Runtime.Hosting;

/// <summary>
/// Provides registration methods for SQLite-backed Orleans grain-state
/// persistence.
/// </summary>
public static class SiloBuilderExtensions {
    /// <summary>
    /// Adds a named SQLite-backed Orleans grain storage provider to a silo.
    /// </summary>
    /// <param name="siloBuilder">
    /// The Orleans silo builder that receives the persistence services.
    /// </param>
    /// <param name="storageProviderName">
    /// The provider name referenced by persistent grain-state registrations.
    /// </param>
    /// <param name="connectionString">
    /// The SQLite connection string used by the grain-state database context.
    /// </param>
    /// <returns>
    /// <paramref name="siloBuilder"/> so additional silo services can be
    /// configured.
    /// </returns>
    /// <remarks>
    /// This method registers one unkeyed pooled
    /// <see cref="IDbContextFactory{TContext}"/> for
    /// <see cref="GrainStateDbContext"/> and one named Orleans storage provider.
    /// Call it once for this context type within a silo service collection.
    /// Registering it repeatedly with different connection strings can cause
    /// all provider instances to resolve the same unkeyed context factory.
    ///
    /// <para>
    /// The connection string can contain file-system or credential information
    /// and must not be written to logs or exposed through diagnostics.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="siloBuilder"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="storageProviderName"/> or
    /// <paramref name="connectionString"/> is empty or consists only of
    /// white-space characters.
    /// </exception>
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
