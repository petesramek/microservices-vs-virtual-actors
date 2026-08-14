namespace Workbench.Gateway.Observability.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Workbench.Contracts;
using Workbench.Gateway.Clients;
using Workbench.Gateway.Configuration;

/// <summary>
/// Reports the caller-specific health of downstream architecture APIs used by
/// Workbench Gateway.
/// </summary>
internal sealed class GatewayDependencyHealthCheck(
    ServiceStatusClient statusClient,
    IOptions<ServiceEndpointOptions> options)
    : IHealthCheck {
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);

        (string DisplayName, string BaseUrl) dependency =
            context.Registration.Name switch {
                "orders-api" => (
                    "Orders API",
                    options.Value.MicroservicesBaseUrl
                ),

                "ordering-api" => (
                    "Ordering API",
                    options.Value.VirtualActorsBaseUrl
                ),

                _ => throw new InvalidOperationException(
                    $"Unsupported Gateway dependency health check " +
                    $"'{context.Registration.Name}'."
                ),
            };

        ServiceStatus status = await statusClient
            .GetAsync(
                dependency.DisplayName,
                dependency.BaseUrl,
                cancellationToken
            )
            .ConfigureAwait(false);

        return status.IsOnline
            ? HealthCheckResult.Healthy(
                $"{dependency.DisplayName} is reachable from Workbench " +
                "Gateway."
            )
            : HealthCheckResult.Unhealthy(
                $"{dependency.DisplayName} is not reachable from Workbench " +
                "Gateway."
            );
    }
}