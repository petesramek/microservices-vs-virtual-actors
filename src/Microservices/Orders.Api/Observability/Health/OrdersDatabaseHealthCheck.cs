namespace Orders.Api.Observability.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orders.Api.Data;

/// <summary>
/// Verifies that the Orders database can accept connections.
/// </summary>
internal sealed class OrdersDatabaseHealthCheck(
    IServiceScopeFactory serviceScopeFactory)
    : IHealthCheck {
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);

        using IServiceScope scope = serviceScopeFactory.CreateScope();

        OrdersDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<OrdersDbContext>();

        try {
            bool canConnect = await dbContext.Database
                .CanConnectAsync(cancellationToken)
                .ConfigureAwait(false);

            return canConnect
                ? HealthCheckResult.Healthy(
                    "The Orders database is available.")
                : HealthCheckResult.Unhealthy(
                    "The Orders database is unavailable.");
        } catch (OperationCanceledException)
              when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            return HealthCheckResult.Unhealthy(
                "The Orders database health check failed.",
                exception);
        }
    }
}
