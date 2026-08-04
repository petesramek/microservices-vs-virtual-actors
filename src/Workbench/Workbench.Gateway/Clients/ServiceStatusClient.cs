namespace Workbench.Gateway.Clients;

using Workbench.Contracts;

/// <summary>
/// Provides operations for retrieving service health status.
/// </summary>
internal sealed class ServiceStatusClient(IHttpClientFactory httpClientFactory) {
    /// <summary>
    /// Gets the health status of the specified service.
    /// </summary>
    /// <param name="name">The display name of the service.</param>
    /// <param name="baseUrl">The base URL of the service.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The current health status of the service.</returns>
    public async Task<ServiceStatus> GetAsync(
        string name,
        string baseUrl,
        CancellationToken cancellationToken) {
        HttpClient httpClient = httpClientFactory.CreateClient();
        var healthUrl = new Uri(
            $"{baseUrl.TrimEnd('/')}/health",
            UriKind.Absolute);

        try {
            using HttpResponseMessage response = await httpClient
                .GetAsync(healthUrl, cancellationToken)
                .ConfigureAwait(false);

            return new ServiceStatus(
                name,
                healthUrl.ToString(),
                response.IsSuccessStatusCode,
                $"{(int)response.StatusCode} {response.StatusCode}",
                response.IsSuccessStatusCode
                    ? null
                    : "Health endpoint returned a non-success status code.");
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            return new ServiceStatus(
                name,
                healthUrl.ToString(),
                IsOnline: false,
                "Unavailable",
                exception.Message);
        }
    }
}
