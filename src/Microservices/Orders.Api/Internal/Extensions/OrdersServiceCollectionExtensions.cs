namespace Orders.Api.Internal.Extensions;

using Microsoft.EntityFrameworkCore;
using Orders.Api.Internal.Clients;
using Orders.Api.Internal.Clients.Abstraction;
using Orders.Api.Internal.Infrastructure;
using Orders.Api.Internal.Observability.Health;

/// <summary>
/// Provides dependency-injection registration for the Orders API.
/// </summary>
internal static class OrdersServiceCollectionExtensions {
    /// <summary>
    /// Registers Orders API persistence, downstream HTTP clients, and health
    /// checks.
    /// </summary>
    /// <param name="services">
    /// The service collection that receives the Orders API registrations.
    /// </param>
    /// <param name="configuration">
    /// The application configuration containing connection strings and
    /// downstream service addresses.
    /// </param>
    /// <returns>The supplied service collection.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configuration"/> is
    /// <see langword="null"/>.
    /// </exception>
    internal static IServiceCollection AddOrdersApi(
        this IServiceCollection services,
        IConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<OrdersDbContext>(options => {
            string connectionString =
                configuration.GetConnectionString("Default")
                ?? "Data Source=orders.db";

            options.UseSqlite(connectionString);
        });

        services.AddHttpClient<IInventoryClient, HttpInventoryClient>(client => {
            string baseUrl =
                configuration["Services:InventoryBaseUrl"]
                ?? "http://localhost:5201";

            client.BaseAddress = new Uri(baseUrl);
        });

        services.AddHttpClient<IPaymentsClient, HttpPaymentsClient>(client => {
            string baseUrl =
                configuration["Services:PaymentsBaseUrl"]
                ?? "http://localhost:5202";

            client.BaseAddress = new Uri(baseUrl);
        });

        services
            .AddHealthChecks()
            .AddCheck<OrdersDatabaseHealthCheck>("orders-database");

        return services;
    }
}
