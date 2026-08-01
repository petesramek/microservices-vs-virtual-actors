using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.UseOrleans(siloBuilder => {
    siloBuilder.UseLocalhostClustering();
});

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
