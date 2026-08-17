namespace Payments.Api;

using Hosting.ServiceDefaults.Extensions;
using Microsoft.EntityFrameworkCore;
using Payments.Api.Extensions;
using Payments.Api.Internal.Infrastructure;
using Payments.Api.Internal.Observability.Health;
using Payments.Api.Internal.Observability.Logging;

/// <summary>
/// Provides the entry point and application composition for the Payments API.
/// </summary>
public class Program {
    /// <summary>
    /// Configures and runs the Payments API host.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the application.</param>
    /// <returns>
    /// A task that represents the lifetime of the application host.
    /// </returns>
    private static async Task Main(string[] args) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();

        builder.Services.AddDbContext<PaymentsDbContext>(options => {
            string connectionString =
                builder.Configuration.GetConnectionString("Default")
                ?? "Data Source=payments.db";

            options.UseSqlite(connectionString);
        });

        builder.Services
            .AddHealthChecks()
            .AddCheck<PaymentsDatabaseHealthCheck>("payments-database");

        WebApplication app = builder.Build();

        app.Use(async (context, next) => {
            string? correlationId = context.Request.Headers["X-Correlation-ID"]
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

        app.MapPaymentsEndpoints();
        app.MapDefaultEndpoints();

        await app.RunAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Ensures that the payments database and its schema exist before requests
    /// are accepted.
    /// </summary>
    /// <param name="services">
    /// The application service provider used to resolve the payments database
    /// context.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous database initialization
    /// operation.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    private static async Task EnsureDatabaseAsync(IServiceProvider services) {
        ArgumentNullException.ThrowIfNull(services);

        AsyncServiceScope scope = services.CreateAsyncScope();

        await using (scope.ConfigureAwait(false)) {
            PaymentsDbContext db = scope.ServiceProvider
            .GetRequiredService<PaymentsDbContext>();

            await db.Database
                .EnsureCreatedAsync()
                .ConfigureAwait(false);
        }
    }
}
