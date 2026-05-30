using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orders.Api.Clients;
using Orders.Api.Data;

namespace Microservices.Tests.Infrastructure;

/// <summary>
/// Test factory for the Orders API with replaceable downstream clients.
/// </summary>
public sealed class OrdersApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"orders-api-tests-{Guid.NewGuid():N}.db");

    /// <summary>
    /// Gets the fake inventory client.
    /// </summary>
    public FakeInventoryClient InventoryClient { get; } = new();

    /// <summary>
    /// Gets the fake payments client.
    /// </summary>
    public FakePaymentsClient PaymentsClient { get; } = new();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={_databasePath}"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<OrdersDbContext>>();
            services.RemoveAll<IInventoryClient>();
            services.RemoveAll<IPaymentsClient>();

            services.AddDbContext<OrdersDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
            services.AddSingleton<IInventoryClient>(InventoryClient);
            services.AddSingleton<IPaymentsClient>(PaymentsClient);
        });
    }
}
