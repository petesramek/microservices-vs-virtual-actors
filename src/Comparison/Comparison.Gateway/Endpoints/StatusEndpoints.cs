namespace Comparison.Gateway.Endpoints;

using Comparison.Contracts;
using Comparison.Gateway.Clients;
using Comparison.Gateway.Configuration;
using Microsoft.Extensions.Options;

/// <summary>
/// Provides endpoint mappings for gateway and architecture service status.
/// </summary>
internal static class StatusEndpoints {
    /// <summary>
    /// Maps the service status endpoint.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapStatusEndpoints(this IEndpointRouteBuilder endpoints) {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/status", GetStatusAsync);

        return endpoints;
    }

    /// <summary>
    /// Gets the current status of the gateway and both architecture services.
    /// </summary>
    /// <param name="statusClient">The service status client.</param>
    /// <param name="options">The configured architecture endpoints.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The gateway and architecture service statuses.</returns>
    private static async Task<IResult> GetStatusAsync(
        ServiceStatusClient statusClient,
        IOptions<ServiceEndpointOptions> options,
        CancellationToken cancellationToken) {
        var gateway = new ServiceStatus("Gateway", "local", IsOnline: true, "Online", Error: null);

        Task<ServiceStatus> microservicesTask = statusClient.GetAsync(
            "Microservices",
            options.Value.MicroservicesBaseUrl,
            cancellationToken);

        Task<ServiceStatus> virtualActorsTask = statusClient.GetAsync(
            "Virtual Actors",
            options.Value.VirtualActorsBaseUrl,
            cancellationToken);

        await Task.WhenAll(
            microservicesTask,
            virtualActorsTask).ConfigureAwait(false);

        return Results.Ok(new BackendStatusResponse(
            gateway,
            await microservicesTask.ConfigureAwait(false),
            await virtualActorsTask.ConfigureAwait(false)));
    }
}
