namespace Workbench.Gateway.Internal.Extensions;

using Microsoft.Extensions.Options;
using Workbench.Gateway.Internal.Clients;
using Workbench.Gateway.Internal.Configuration;

/// <summary>
/// Provides dependency-injection registration for downstream service clients.
/// </summary>
public static class ServiceClientServiceCollectionExtensions {
    /// <summary>
    /// Registers validated service endpoint options and typed HTTP clients for
    /// the supported service implementations.
    /// </summary>
    /// <param name="services">
    /// The service collection to which the service clients are added.
    /// </param>
    /// <param name="configuration">
    /// The configuration section containing downstream service endpoint
    /// settings.
    /// </param>
    /// <returns>
    /// The same service collection so that additional registrations can be
    /// chained.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddServiceClients(
        this IServiceCollection services,
        IConfigurationSection configuration) {
        ArgumentNullException.ThrowIfNull(services);

        // Configure and validate downstream architecture endpoints.
        services
            .AddOptions<ServiceEndpointOptions>()
            .Bind(configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Configure microservices service client.
        services.AddHttpClient<MicroservicesServiceClient>(
            (services, client) => {
                ServiceEndpointOptions options = services
                    .GetRequiredService<IOptions<ServiceEndpointOptions>>()
                    .Value;

                client.BaseAddress = new Uri(options.MicroservicesBaseUrl);
            });

        // Configure virtual actor service client.
        services.AddHttpClient<VirtualActorsServiceClient>(
            (services, client) => {
                ServiceEndpointOptions options = services
                    .GetRequiredService<IOptions<ServiceEndpointOptions>>()
                    .Value;

                client.BaseAddress = new Uri(options.VirtualActorsBaseUrl);
            });

        return services;
    }
}
