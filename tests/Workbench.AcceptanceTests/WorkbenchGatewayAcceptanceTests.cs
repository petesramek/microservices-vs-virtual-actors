namespace Workbench.AcceptanceTests;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using Workbench.Contracts.Inventory;
using Workbench.Contracts.Orders;
using Workbench.Contracts.Scenarios;
using Workbench.Gateway;
using Workbench.Gateway.Internal.Clients;
using Xunit;

/// <summary>
/// Acceptance tests for the workbench gateway contract.
/// </summary>
public sealed class WorkbenchGatewayAcceptanceTests {
    private static WebApplicationFactory<Program> CreateFactory() {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => {
                builder.ConfigureTestServices(services => {
                    services.AddHttpClient<MicroservicesServiceClient>()
                        .ConfigurePrimaryHttpMessageHandler(() => new FakeArchitectureHandler());

                    services.AddHttpClient<VirtualActorsServiceClient>()
                        .ConfigurePrimaryHttpMessageHandler(() => new FakeArchitectureHandler());
                });
            });
    }

    private sealed class FakeArchitectureHandler : HttpMessageHandler {
        private readonly Dictionary<string, int> _inventory = new(StringComparer.OrdinalIgnoreCase);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (string.Equals(path, "/api/scenarios/reset", StringComparison.OrdinalIgnoreCase)) {
                ResetInventoryRequest reset = request.Content!.ReadFromJsonAsync<ResetInventoryRequest>(cancellationToken).GetAwaiter().GetResult()!;
                _inventory[reset.ProductId] = reset.Quantity;
                return Json(new InventoryResponse(reset.ProductId, reset.Quantity));
            }

            if (path.StartsWith("/api/inventory/", StringComparison.OrdinalIgnoreCase)) {
                var productId = path.Split('/').Last();
                return Json(new InventoryResponse(productId, _inventory.GetValueOrDefault(productId)));
            }

            if (string.Equals(path, "/api/orders", StringComparison.OrdinalIgnoreCase)) {
                RunScenarioRequest order = request.Content!.ReadFromJsonAsync<RunScenarioRequest>(cancellationToken).GetAwaiter().GetResult()!;
                var available = _inventory.GetValueOrDefault(order.ProductId);
                if (available < order.Quantity) {
                    return Json(new OrderResponse(order.OrderId, OrderStatus.Rejected, "InsufficientInventory"));
                }

                if (order.SimulatePaymentFailure) {
                    return Json(new OrderResponse(order.OrderId, OrderStatus.Rejected, "PaymentFailed"));
                }

                _inventory[order.ProductId] = available - order.Quantity;
                return Json(new OrderResponse(order.OrderId, OrderStatus.Completed, Reason: null));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static Task<HttpResponseMessage> Json<T>(T value) {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = JsonContent.Create(value),
            });
        }
    }
}


