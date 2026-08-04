using Hosting.ServiceDefaults.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ordering.Persistence.Sqlite.Extensions;
using Ordering.Silo.HealthChecks;
using Orleans.Dashboard;

const string ConnectionName = "Default";
const string StorageProviderName = "OrderingStorage";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

string connectionString =
    builder.Configuration.GetConnectionString(ConnectionName)
    ?? throw new InvalidOperationException(
        "The Default database connection string is not configured.");

builder.Services
    .AddHealthChecks()
    .AddCheck<OrderingDatabaseHealthCheck>("ordering-database");

builder.UseOrleans(siloBuilder => {
    siloBuilder
        .UseLocalhostClustering()
        .AddActivityPropagation()
        .AddDashboard();

    siloBuilder.AddSqliteGrainStorage(
        StorageProviderName,
        connectionString);
});

WebApplication app = builder.Build();

app.MapOrleansDashboard("/dashboard");
app.MapDefaultEndpoints();

await app
    .RunAsync()
    .ConfigureAwait(false);

public partial class Program;
