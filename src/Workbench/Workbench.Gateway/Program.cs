using Hosting.ServiceDefaults.Extensions;
using Hosting.ServiceDefaults.Observability.Metrics;
using Microsoft.Extensions.Options;
using Workbench.Gateway.Internal.Clients;
using Workbench.Gateway.Internal.Configuration;
using Workbench.Gateway.Internal.Endpoints;
using Workbench.Gateway.Internal.Extensions;
using Workbench.Gateway.Internal.Runners;

namespace Workbench.Gateway;

/// <summary>
/// Provides the entry point and application composition for the workbench
/// gateway.
/// </summary>
public class Program {
    /// <summary>
    /// Configures gateway services and the HTTP request pipeline, maps the
    /// gateway endpoints, and runs the application host.
    /// </summary>
    /// <param name="args">
    /// The command-line arguments passed to the web application builder.
    /// </param>
    /// <returns>
    /// A task that represents the lifetime of the running application host.
    /// </returns>
    private static async Task Main(string[] args) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Add shared Aspire service discovery, resilience, health checks, and OpenTelemetry.
        builder.AddServiceDefaults();

        // Configure standardized error responses.
        builder.Services.AddProblemDetails();

        // Configure and validate downstream architecture endpoints.
        builder.Services
            .AddOptions<ServiceEndpointOptions>()
            .Bind(builder.Configuration.GetSection(
                ServiceEndpointOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Configure microservices service client.
        builder.Services.AddHttpClient<MicroservicesServiceClient>(
            (services, client) => {
                ServiceEndpointOptions options = services
                    .GetRequiredService<IOptions<ServiceEndpointOptions>>()
                    .Value;

                client.BaseAddress = new Uri(options.MicroservicesBaseUrl);
            });

        // Configure virtual actor service client.
        builder.Services.AddHttpClient<VirtualActorsServiceClient>(
            (services, client) => {
                ServiceEndpointOptions options = services
                    .GetRequiredService<IOptions<ServiceEndpointOptions>>()
                    .Value;

                client.BaseAddress = new Uri(options.VirtualActorsBaseUrl);
            });

        // Register caller-specific downstream dependency health checks.
        builder.Services.AddHealthChecks();

        // Configure gateway services.
        builder.Services.AddSingleton<ScenarioRunner>();

        // Register workbench metrics.
        builder.Services.AddSingleton<ScenarioMetrics>();

        WebApplication app = builder.Build();

        // Configure the request pipeline.
        app.UseCorrelationId();
        app.UseExceptionHandler();

        // Map gateway endpoints.
        app.MapGet("/", () => Results.Ok(new {
            Name = "Workbench Gateway",
            Description = "Routes scenario requests by X-Architecture header.",
        }));

        //app.MapStatusEndpoints();
        app.MapScenarioEndpoints();

        // Map the shared health and aliveness endpoints.
        app.MapDefaultEndpoints();

        await app.RunAsync().ConfigureAwait(false);
    }
}
