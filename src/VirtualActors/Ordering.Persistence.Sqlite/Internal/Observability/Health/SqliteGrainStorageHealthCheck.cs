namespace Ordering.Persistence.Sqlite.Internal.Observability.Health;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Ordering.Persistence.Sqlite.Internal.Infrastructure;

/// <summary>
/// Verifies that the SQLite database configured for Orleans grain-state
/// persistence is available for connections.
/// </summary>
/// <param name="dbContextFactory">
/// The factory used to create an independent grain-state database context.
/// </param>
internal sealed class SqliteGrainStorageHealthCheck(
    IDbContextFactory<GrainStateDbContext> dbContextFactory)
    : IHealthCheck {
    /// <summary>
    /// Checks whether the configured SQLite database can be connected to.
    /// </summary>
    /// <param name="context">The health-check execution context.</param>
    /// <param name="cancellationToken">
    /// The token that cancels the health-check operation.
    /// </param>
    /// <returns>
    /// A task whose result is healthy when the database accepts a connection;
    /// otherwise, unhealthy with a non-sensitive failure description.
    /// </returns>
    /// <remarks>
    /// This check verifies connectivity only. Database migrations remain
    /// responsible for creating and updating the grain-state schema.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled by the caller or by the
    /// health-check registration timeout.
    /// </exception>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);

        try {
            GrainStateDbContext dbContext = await dbContextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            await using (dbContext.ConfigureAwait(false)) {
                bool canConnect = await dbContext.Database
                    .CanConnectAsync(cancellationToken)
                    .ConfigureAwait(false);

                return canConnect
                    ? HealthCheckResult.Healthy(
                        "The SQLite grain-state database is available.")
                    : HealthCheckResult.Unhealthy(
                        "The SQLite grain-state database is unavailable.");
            }
        } catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            return HealthCheckResult.Unhealthy(
                "The SQLite grain-state database health check failed.",
                exception);
        }
    }
}
