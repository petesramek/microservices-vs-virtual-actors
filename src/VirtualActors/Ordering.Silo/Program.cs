using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Add shared Aspire service discovery, resilience, health checks, and OpenTelemetry.
builder.AddServiceDefaults();

builder.UseOrleans(siloBuilder => {
    siloBuilder.UseLocalhostClustering();
});

IHost host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
