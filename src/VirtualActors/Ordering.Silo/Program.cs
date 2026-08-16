using Hosting.ServiceDefaults.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Ordering.Persistence.Sqlite.Extensions;
using Orleans.Dashboard;

namespace Ordering.Silo;

/// <summary>
/// Configures and runs the web host for the local Orleans ordering silo.
/// </summary>
/// <remarks>
/// The host uses localhost clustering, persists grain state through the named
/// SQLite storage provider, exposes the Orleans Dashboard, and maps the shared
/// readiness and liveness endpoints.
/// </remarks>
public class Program {
    /// <summary>
    /// Identifies the database connection string used by the SQLite grain
    /// storage provider.
    /// </summary>
    private const string ConnectionName = "Default";

    /// <summary>
    /// Identifies the named Orleans grain storage provider used by ordering
    /// grains.
    /// </summary>
    private const string StorageProviderName = "OrderingStorage";

    /// <summary>
    /// Identifies the health check that verifies connectivity to the SQLite
    /// grain-state database.
    /// </summary>
    private const string DatabaseHealthCheckName = "ordering-database";

    /// <summary>
    /// Identifies the route prefix used by the co-hosted Orleans Dashboard.
    /// </summary>
    private const string DashboardRoutePrefix = "/dashboard";

    /// <summary>
    /// Configures the ordering silo and runs the web application until
    /// shutdown.
    /// </summary>
    /// <param name="args">
    /// Command-line arguments forwarded to the web application builder.
    /// </param>
    /// <returns>
    /// A task that represents the lifetime of the running web application.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The connection string identified by <see cref="ConnectionName"/> is not
    /// configured.
    /// </exception>
    private static async Task Main(string[] args) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();

        string connectionString =
            builder.Configuration.GetConnectionString(ConnectionName)
            ?? throw new InvalidOperationException(
                $"The '{ConnectionName}' database connection string is not " +
                "configured.");

        builder.UseOrleans(siloBuilder => {
            siloBuilder
                .UseLocalhostClustering()
                .AddActivityPropagation()
                .AddDashboard()
                .AddSqliteGrainStorage(
                    StorageProviderName,
                    connectionString)
                .AddSqliteGrainStorageHealthCheck(
                    DatabaseHealthCheckName);
        });

        WebApplication app = builder.Build();

        app.MapOrleansDashboard(DashboardRoutePrefix);
        app.MapDefaultEndpoints();

        await app
            .RunAsync()
            .ConfigureAwait(false);
    }
}
