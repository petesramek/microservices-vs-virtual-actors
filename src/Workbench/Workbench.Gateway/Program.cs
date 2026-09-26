using Hosting.ServiceDefaults.Extensions;
using Hosting.ServiceDefaults.Observability.Metrics;
using Workbench.Gateway.Internal.Configuration;
using Workbench.Gateway.Internal.Endpoints;
using Workbench.Gateway.Internal.Extensions;

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

        builder.Services.AddServiceClients(builder.Configuration.GetSection(ServiceEndpointOptions.SectionName));

        // Register caller-specific downstream dependency health checks.
        builder.Services.AddHealthChecks();

        // Register scenario runners.
        builder.Services.AddScenarioRunners();

        // Register workbench metrics.
        builder.Services.AddSingleton<ScenarioMetrics>();

        WebApplication app = builder.Build();

        // Configure the request pipeline.
        app.UseCorrelationId();
        app.UseExceptionHandler();

        // Map gateway endpoints.
        app.MapGet("/", () => Results.Ok(new {
            Name = "Workbench Gateway",
            Description = "Routes scenario requests.",
        }));

        app.MapScenarioEndpoints();

        // Map the shared health and aliveness endpoints.
        app.MapDefaultEndpoints();

        await app.RunAsync().ConfigureAwait(false);
    }
}
