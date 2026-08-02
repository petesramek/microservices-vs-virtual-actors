using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ordering.Persistence.Sqlite.Extensions;

const string ConnectionName = "Default";
const string StorageProviderName = "OrderingStorage";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string connectionString =
    builder.Configuration.GetConnectionString(ConnectionName)
    ?? throw new InvalidOperationException(
        "The Default database connection string is not configured.");

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Host.UseOrleans(siloBuilder => {
    siloBuilder.UseLocalhostClustering();

    siloBuilder.AddSqliteGrainStorage(
        StorageProviderName,
        connectionString);
});

public partial class Program;
