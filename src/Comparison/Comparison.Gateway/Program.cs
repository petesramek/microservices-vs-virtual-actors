using Comparison.Gateway.Clients;
using Comparison.Gateway.Configuration;
using Comparison.Gateway.Endpoints;
using Comparison.Gateway.Extensions;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Keep console logging as the single provider so local runs and a future Aspire dashboard receive the same events.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Produce standardized problem-details responses for unhandled gateway failures.
builder.Services.AddProblemDetails();

// Encapsulate service health checks and their status mapping.
builder.Services.AddSingleton<ServiceStatusClient>();

// Bind and validate the downstream architecture endpoints during application startup.
builder.Services
    .AddOptions<ArchitectureEndpointOptions>()
    .Bind(builder.Configuration.GetSection(ArchitectureEndpointOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Configure the client that runs scenarios through the Microservices architecture.
builder.Services.AddHttpClient<MicroservicesArchitectureClient>((services, client) => {
    ArchitectureEndpointOptions options = services
        .GetRequiredService<IOptions<ArchitectureEndpointOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.MicroservicesBaseUrl);
});

// Configure the client that runs scenarios through the Virtual Actors architecture.
builder.Services.AddHttpClient<VirtualActorsArchitectureClient>((services, client) => {
    ArchitectureEndpointOptions options = services
        .GetRequiredService<IOptions<ArchitectureEndpointOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.VirtualActorsBaseUrl);
});

WebApplication app = builder.Build();

// Establish one correlation identifier for the gateway request and all downstream calls.
app.UseCorrelationId();

// Convert unhandled failures outside the explicitly handled scenario flow into problem-details responses.
app.UseExceptionHandler();

// Describe the gateway and its architecture-selection contract.
app.MapGet("/", () => Results.Ok(new {
    Name = "Architecture Comparison Gateway",
    Description = "Routes scenario requests by X-Architecture header.",
}));

// Report the current reachability of the gateway and both architecture services.
app.MapStatusEndpoints();

// Run the selected comparison scenario against one or both architectures.
app.MapScenarioEndpoints();

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
