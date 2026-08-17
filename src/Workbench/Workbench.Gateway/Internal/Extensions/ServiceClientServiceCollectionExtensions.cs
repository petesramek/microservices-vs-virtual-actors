namespace Workbench.Gateway.Internal.Extensions;

using Microsoft.Extensions.Options;
using Workbench.Gateway.Internal.Clients;
using Workbench.Gateway.Internal.Configuration;

public static class ServiceClientServiceCollectionExtensions {
    public static IServiceCollection AddServiceClients(this IServiceCollection services, IConfigurationSection configuration) {
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
