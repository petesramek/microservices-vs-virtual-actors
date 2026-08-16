using Hosting.ServiceDefaults.Extensions;
using Orders.Api.Extensions;
using Orders.Api.Internal.Infrastructure;

/// <summary>
/// Configures and runs the Orders API.
/// </summary>
public class Program {
    /// <summary>
    /// Configures services and the request pipeline, initializes persistence,
    /// maps endpoints, and runs the application.
    /// </summary>
    /// <param name="args">
    /// The command-line arguments passed to the web application builder.
    /// </param>
    /// <returns>
    /// A task that represents the lifetime of the running web application.
    /// </returns>
    private static async Task Main(string[] args) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();
        builder.Services.AddOrdersApi(builder.Configuration);

        WebApplication app = builder.Build();

        app.UseCorrelationLogging();
        app.UseOrderIdempotencyGate();

        app.MapOrdersEndpoints();
        app.MapDefaultEndpoints();

        await EnsureDatabaseAsync(app.Services).ConfigureAwait(false);

        await app.RunAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Ensures that the orders database and its schema exist before requests are
    /// accepted.
    /// </summary>
    /// <param name="services">
    /// The application service provider used to resolve the orders database
    /// context.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous database initialization operation.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    private static async Task EnsureDatabaseAsync(IServiceProvider services) {
        ArgumentNullException.ThrowIfNull(services);

        using IServiceScope scope = services.CreateScope();
        OrdersDbContext db = scope.ServiceProvider
            .GetRequiredService<OrdersDbContext>();

        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
    }
}
