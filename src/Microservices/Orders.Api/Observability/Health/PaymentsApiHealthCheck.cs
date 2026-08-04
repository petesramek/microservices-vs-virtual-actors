namespace Orders.Api.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Verifies that the Payments API is ready to process requests.
/// </summary>
internal sealed class PaymentsApiHealthCheck(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
    : IHealthCheck {
    private const string BaseUrlConfigurationKey =
        "Services:PaymentsBaseUrl";
    private const string DefaultBaseUrl = "http://localhost:5202";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);

        string baseUrl = configuration[BaseUrlConfigurationKey]
            ?? DefaultBaseUrl;

        return CheckEndpointAsync(baseUrl, cancellationToken);
    }

    private async Task<HealthCheckResult> CheckEndpointAsync(
        string baseUrl,
        CancellationToken cancellationToken) {
        using var timeoutCancellationTokenSource =
            new CancellationTokenSource(Timeout);
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellationTokenSource.Token);

        try {
            HttpClient client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri(new Uri(baseUrl), "/health"));
            using HttpResponseMessage response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                linkedCancellationTokenSource.Token).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy(
                    "The Payments API is available.")
                : HealthCheckResult.Unhealthy(
                    $"The Payments API returned status code {(int)response.StatusCode}.");
        } catch (OperationCanceledException)
              when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (OperationCanceledException exception) {
            return HealthCheckResult.Unhealthy(
                "The Payments API health check timed out.",
                exception);
        } catch (Exception exception) {
            return HealthCheckResult.Unhealthy(
                "The Payments API health check failed.",
                exception);
        }
    }
}
