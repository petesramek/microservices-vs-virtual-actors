namespace Inventory.Api.Internal.Observability.Health;

using Inventory.Api.Internal.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Verifies that the inventory database is reachable.
/// </summary>
/// <remarks>
/// Each execution creates a dependency-injection scope, resolves a scoped
/// <see cref="InventoryDbContext"/>, and attempts to open a database connection.
/// Cancellation requested through the supplied token is propagated to the
/// caller rather than converted into an unhealthy result.
/// </remarks>
internal sealed class InventoryDatabaseHealthCheck(
    IServiceScopeFactory serviceScopeFactory)
    : IHealthCheck {
    /// <summary>
    /// Runs the inventory database connectivity check.
    /// </summary>
    /// <param name="context">
    /// The context associated with the current health-check execution.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the database connectivity check.
    /// </param>
    /// <returns>
    /// A task that yields a healthy result when the database can be reached, or
    /// an unhealthy result when connectivity fails or an unexpected exception
    /// occurs.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled while the check is
    /// running.
    /// </exception>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);

        using IServiceScope scope = serviceScopeFactory.CreateScope();
        InventoryDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();

        try {
            bool canConnect = await dbContext.Database
                .CanConnectAsync(cancellationToken)
                .ConfigureAwait(false);

            return canConnect
                ? HealthCheckResult.Healthy(
                    "The Inventory database is available.")
                : HealthCheckResult.Unhealthy(
                    "The Inventory database is unavailable.");
        } catch (OperationCanceledException)
              when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            return HealthCheckResult.Unhealthy(
                "The Inventory database health check failed.",
                exception);
        }
    }
}
