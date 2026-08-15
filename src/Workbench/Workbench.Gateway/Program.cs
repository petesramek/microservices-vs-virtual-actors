using Hosting.ServiceDefaults.Extensions;
using Microsoft.Extensions.Options;
using Workbench.Gateway.Clients;
using Workbench.Gateway.Configuration;
using Workbench.Gateway.Endpoints;
using Workbench.Gateway.Extensions;
using Workbench.Gateway.Scenarios;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add shared Aspire service discovery, resilience, health checks, and OpenTelemetry.
builder.AddServiceDefaults();

// Configure standardized error responses.
builder.Services.AddProblemDetails();

// Configure and validate downstream architecture endpoints.
builder.Services
    .AddOptions<ServiceEndpointOptions>()
    .Bind(builder.Configuration.GetSection(ServiceEndpointOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Configure microservices service client.
builder.Services.AddHttpClient<MicroservicesServiceClient>((services, client) => {
    ServiceEndpointOptions options = services
        .GetRequiredService<IOptions<ServiceEndpointOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.MicroservicesBaseUrl);
});

// Configure virtual actor service client.
builder.Services.AddHttpClient<VirtualActorsServiceClient>((services, client) => {
    ServiceEndpointOptions options = services
        .GetRequiredService<IOptions<ServiceEndpointOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.VirtualActorsBaseUrl);
});

// Register caller-specific downstream dependency health checks.
builder.Services
    .AddHealthChecks()
    //.AddCheck<GatewayDependencyHealthCheck>("orders-api")
    //.AddCheck<GatewayDependencyHealthCheck>("ordering-api")
    ;
// Configure gateway services.
builder.Services.AddSingleton<ServiceStatusClient>();
builder.Services.AddSingleton<ScenarioRunner>();

WebApplication app = builder.Build();

// Configure the request pipeline.
app.UseCorrelationId();
app.UseExceptionHandler();

// Map gateway endpoints.
app.MapGet("/", () => Results.Ok(new {
    Name = "Architecture Workbench Gateway",
    Description = "Routes scenario requests by X-Architecture header.",
}));

app.MapStatusEndpoints();
app.MapScenarioEndpoints();

// Map the shared health and aliveness endpoints.
app.MapDefaultEndpoints();

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
