namespace Ordering.Api;

using Hosting.ServiceDefaults.Extensions;
using Ordering.Api.Extensions;
using Ordering.Api.Internal.Observability.Logging;

internal class Program {
    private static async Task Main(string[] args) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();

        builder.UseOrleansClient(clientBuilder => {
            clientBuilder
                .UseLocalhostClustering()
                .AddActivityPropagation();
        });

        builder.Services.AddHealthChecks();

        WebApplication app = builder.Build();

        app.Use(async (context, next) => {
            string? correlationId =
                context.Request.Headers["X-Correlation-ID"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(correlationId)) {
                await next().ConfigureAwait(false);
                return;
            }

            using IDisposable? scope = app.Logger.BeginScope(
                new Dictionary<string, object>(StringComparer.Ordinal) {
                    ["CorrelationId"] = correlationId,
                });

            app.Logger.HandlingRequestWithCorrelationId(correlationId);

            await next().ConfigureAwait(false);
        });

        app.MapOrderingEndpoints();
        app.MapDefaultEndpoints();

        await app
            .RunAsync()
            .ConfigureAwait(false);
    }
}

/// <summary>
/// Provides the entry point marker used by integration tests and hosting tools.
/// </summary>

