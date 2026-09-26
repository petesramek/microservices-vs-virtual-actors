namespace Ordering.Persistence.Sqlite.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Ordering.Persistence.Sqlite.Internal.Infrastructure;
using Ordering.Persistence.Sqlite.Internal.Observability.Health;
using Orleans.Hosting;
using Orleans.Runtime.Hosting;

/// <summary>
/// Provides registration methods for SQLite-backed Orleans grain-state
/// persistence and its connectivity health check.
/// </summary>
public static class SiloBuilderExtensions {
    /// <summary>
    /// Defines the default name of the SQLite grain-storage health check.
    /// </summary>
    private const string DefaultHealthCheckName = "sqlite-grain-storage";

    /// <summary>
    /// Defines the default maximum duration of the SQLite grain-storage health
    /// check.
    /// </summary>
    private static readonly TimeSpan DefaultHealthCheckTimeout =
        TimeSpan.FromSeconds(2);

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
    /// Call <see cref="AddSqliteGrainStorageHealthCheck"/> after this method to
    /// register the associated connectivity health check.
    /// </para>
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

    /// <summary>
    /// Adds a connectivity health check for the configured SQLite grain-state
    /// database.
    /// </summary>
    /// <param name="siloBuilder">
    /// The Orleans silo builder that receives the health-check registration.
    /// </param>
    /// <param name="healthCheckName">
    /// The health-check registration name. When <see langword="null"/>,
    /// <c>sqlite-grain-storage</c> is used.
    /// </param>
    /// <param name="timeout">
    /// The maximum health-check duration. When <see langword="null"/>, two
    /// seconds is used.
    /// </param>
    /// <returns>
    /// <paramref name="siloBuilder"/> so additional silo services can be
    /// configured.
    /// </returns>
    /// <remarks>
    /// Register SQLite grain storage with
    /// <see cref="AddSqliteGrainStorage"/> before calling this method so the
    /// health check can resolve the registered
    /// <see cref="IDbContextFactory{TContext}"/>. The check uses
    /// <see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.CanConnectAsync(CancellationToken)"/>
    /// and verifies connectivity only; it does not validate schema freshness.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="siloBuilder"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="healthCheckName"/> is empty or consists only of
    /// white-space characters.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="timeout"/> is less than or equal to
    /// <see cref="TimeSpan.Zero"/>.
    /// </exception>
    public static ISiloBuilder AddSqliteGrainStorageHealthCheck(
        this ISiloBuilder siloBuilder,
        string? healthCheckName = null,
        TimeSpan? timeout = null) {
        ArgumentNullException.ThrowIfNull(siloBuilder);

        string resolvedHealthCheckName =
            healthCheckName ?? DefaultHealthCheckName;
        ArgumentException.ThrowIfNullOrWhiteSpace(
            resolvedHealthCheckName,
            nameof(healthCheckName));

        TimeSpan resolvedTimeout =
            timeout ?? DefaultHealthCheckTimeout;
        if (resolvedTimeout <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "The health-check timeout must be greater than zero.");
        }

        siloBuilder.Services
            .AddHealthChecks()
            .AddCheck<SqliteGrainStorageHealthCheck>(
                resolvedHealthCheckName,
                failureStatus: HealthStatus.Unhealthy,
                timeout: resolvedTimeout);

        return siloBuilder;
    }
}
