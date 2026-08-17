namespace Inventory.Api;

using Hosting.ServiceDefaults.Extensions;
using Inventory.Api.Extensions;
using Inventory.Api.Internal.Infrastructure;
using Inventory.Api.Internal.Observability.Health;
using Inventory.Api.Internal.Observability.Logging;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Configures and runs the inventory microservice.
/// </summary>
internal sealed class Program {
    /// <summary>
    /// Configures service defaults, persistence, health checks, middleware, and
    /// inventory endpoints, then runs the web application.
    /// </summary>
    /// <param name="args">
    /// Command-line arguments forwarded to the web application builder.
    /// </param>
    /// <returns>
    /// A task that represents the lifetime of the running web application.
    /// </returns>
    private static async Task Main(string[] args) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();

        builder.Services.AddDbContext<InventoryDbContext>(options => {
            string connectionString =
                builder.Configuration.GetConnectionString("Default")
                ?? "Data Source=inventory.db";

            options.UseSqlite(connectionString);
        });

        builder.Services
            .AddHealthChecks()
            .AddCheck<InventoryDatabaseHealthCheck>("inventory-database");

        WebApplication app = builder.Build();

        app.Use(async (context, next) => {
            string? correlationId = context.Request
                .Headers["X-Correlation-ID"]
                .FirstOrDefault();

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

        await EnsureDatabaseAsync(app.Services).ConfigureAwait(false);

        app.MapInventoryEndpoints();
        // Map the shared health and aliveness endpoints.
        app.MapDefaultEndpoints();

        await app.RunAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Ensures that the inventory database has been created before the service
    /// starts accepting requests.
    /// </summary>
    /// <param name="services">
    /// The application service provider used to resolve the database context.
    /// </param>
    /// <returns>A task that represents the database initialization operation.</returns>
    private static async Task EnsureDatabaseAsync(IServiceProvider services) {
        AsyncServiceScope scope = services.CreateAsyncScope();

        await using (scope.ConfigureAwait(false)) {
            InventoryDbContext db = scope.ServiceProvider
                .GetRequiredService<InventoryDbContext>();

            await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
        }
    }
}
