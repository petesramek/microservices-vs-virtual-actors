namespace Workbench.Gateway.Observability.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Workbench.Gateway.Configuration;

/// <summary>
/// Checks the readiness endpoint of a downstream architecture API.
/// </summary>
internal sealed class DownstreamEndpointHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptions<ServiceEndpointOptions> options,
    DownstreamEndpoint endpoint)
    : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        string baseUrl = endpoint switch
        {
            DownstreamEndpoint.Microservices =>
                options.Value.MicroservicesBaseUrl,

            DownstreamEndpoint.VirtualActors =>
                options.Value.VirtualActorsBaseUrl,

            _ => throw new InvalidOperationException(
                $"Unsupported downstream endpoint '{endpoint}'."),
        };

        Uri healthEndpoint = new(
            new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute),
            "health");

        using var timeoutSource =
            new CancellationTokenSource(Timeout);
        using var linkedSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token);

        try
        {
            HttpClient client = httpClientFactory.CreateClient();
            using HttpResponseMessage response = await client
                .GetAsync(healthEndpoint, linkedSource.Token)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy(
                    $"The downstream endpoint '{endpoint}' is healthy.")
                : HealthCheckResult.Unhealthy(
                    $"The downstream endpoint '{endpoint}' returned " +
                    $"status code {(int)response.StatusCode}.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            return HealthCheckResult.Unhealthy(
                $"The downstream endpoint '{endpoint}' timed out.",
                exception);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                $"The downstream endpoint '{endpoint}' could not be reached.",
                exception);
        }
    }
}

/// <summary>
/// Identifies a downstream architecture endpoint exposed by the gateway.
/// </summary>
internal enum DownstreamEndpoint
{
    /// <summary>
    /// The microservices implementation endpoint.
    /// </summary>
    Microservices,

    /// <summary>
    /// The virtual-actors implementation endpoint.
    /// </summary>
    VirtualActors,
}
