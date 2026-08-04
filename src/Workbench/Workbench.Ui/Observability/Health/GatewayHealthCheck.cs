namespace Workbench.Ui.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Verifies that the Workbench Gateway is ready to accept requests.
/// </summary>
internal sealed class GatewayHealthCheck(
    IHttpClientFactory httpClientFactory)
    : IHealthCheck {
    /// <summary>
    /// The name of the HTTP client used by the health check.
    /// </summary>
    public const string HttpClientName = "GatewayHealthCheck";

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
            HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName);

            using HttpResponseMessage response = await httpClient
                .GetAsync("health", linkedCancellationTokenSource.Token)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy(
                    "The Workbench Gateway is available.")
                : HealthCheckResult.Unhealthy(
                    $"The Workbench Gateway returned status code {(int)response.StatusCode}.");
        } catch (OperationCanceledException)
              when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (OperationCanceledException exception) {
            return HealthCheckResult.Unhealthy(
                "The Workbench Gateway health check timed out.",
                exception);
        } catch (Exception exception) {
            return HealthCheckResult.Unhealthy(
                "The Workbench Gateway health check failed.",
                exception);
        }
    }
}
