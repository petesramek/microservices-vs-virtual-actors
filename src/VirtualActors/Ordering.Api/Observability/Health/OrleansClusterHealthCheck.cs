namespace Ordering.Api.Observability.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orleans.Runtime;

/// <summary>
/// Verifies that the Orleans client can communicate with an active silo.
/// </summary>
internal sealed class OrleansClusterHealthCheck(
    IClusterClient clusterClient)
    : IHealthCheck {
    private const long ManagementGrainKey = 0;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);

        using var timeoutCancellationTokenSource =
            new CancellationTokenSource(Timeout);
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellationTokenSource.Token);

        try {
            IManagementGrain managementGrain = clusterClient
                .GetGrain<IManagementGrain>(ManagementGrainKey);

            Dictionary<SiloAddress, SiloStatus> activeSilos =
                await managementGrain
                    .GetHosts(onlyActive: true)
                    .WaitAsync(linkedCancellationTokenSource.Token)
                    .ConfigureAwait(false);

            return activeSilos.Count > 0
                ? HealthCheckResult.Healthy(
                    "The Orleans cluster is available.")
                : HealthCheckResult.Unhealthy(
                    "The Orleans cluster has no active silos.");
        } catch (OperationCanceledException)
              when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (OperationCanceledException exception) {
            return HealthCheckResult.Unhealthy(
                "The Orleans cluster health check timed out.",
                exception);
        } catch (Exception exception) {
            return HealthCheckResult.Unhealthy(
                "The Orleans cluster health check failed.",
                exception);
        }
    }
}
