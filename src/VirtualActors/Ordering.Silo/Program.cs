using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.UseOrleans(siloBuilder => {
    siloBuilder.UseLocalhostClustering();
});

IHost host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
