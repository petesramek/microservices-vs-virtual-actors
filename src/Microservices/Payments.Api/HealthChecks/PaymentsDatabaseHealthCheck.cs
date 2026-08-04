namespace Payments.Api.HealthChecks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Payments.Api.Data;

/// <summary>
/// Verifies that the Payments database can accept connections.
/// </summary>
internal sealed class PaymentsDatabaseHealthCheck(
    IServiceScopeFactory serviceScopeFactory)
    : IHealthCheck {
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);

        using IServiceScope scope = serviceScopeFactory.CreateScope();

        PaymentsDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<PaymentsDbContext>();

        try {
            bool canConnect = await dbContext.Database
                .CanConnectAsync(cancellationToken)
                .ConfigureAwait(false);

            return canConnect
                ? HealthCheckResult.Healthy(
                    "The Payments database is available.")
                : HealthCheckResult.Unhealthy(
                    "The Payments database is unavailable.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (Exception exception) {
            return HealthCheckResult.Unhealthy(
                "The Payments database health check failed.",
                exception);
        }
    }
}
