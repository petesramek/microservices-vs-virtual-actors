namespace Inventory.Api.Observability.Health;

using Inventory.Api.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Verifies that the Inventory database can accept connections.
/// </summary>
internal sealed class InventoryDatabaseHealthCheck(
    IServiceScopeFactory serviceScopeFactory)
    : IHealthCheck {
    /// <inheritdoc />
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
