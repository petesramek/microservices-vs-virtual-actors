namespace Comparison.AcceptanceTests;

using Comparison.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using Xunit;

/// <summary>
/// Acceptance tests for the comparison gateway contract.
/// </summary>
public sealed class ComparisonGatewayAcceptanceTests {
    [Theory]
    [InlineData($"microservices")]
    [InlineData($"virtual-actors")]
    [InlineData($"both")]
    public async Task GatewayRunSelectedArchitecture(string architecture) {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/scenarios/run") {
            Content = JsonContent.Create(new RunScenarioRequest {
                Scenario = ScenarioKind.SuccessfulOrder,
                ProductId = $"product-001",
                InitialStock = 10,
                Quantity = 2,
            }),
        };
        request.Headers.Add($"X-Architecture", architecture);

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RunScenarioResponse>();
        result.ShouldNotBeNull();

        if (architecture is $"microservices"or $"both") {
            result!.Microservices.ShouldNotBeNull();
        }

        if (architecture is $"virtual-actors"or $"both") {
            result!.VirtualActors.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task GatewayRejectUnknownArchitectureHeader() {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/scenarios/run") {
            Content = JsonContent.Create(new RunScenarioRequest()),
        };
        request.Headers.Add($"X-Architecture", $"unknown");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static WebApplicationFactory<Program> CreateFactory() {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => {
                builder.ConfigureTestServices(services => {
                    services.AddHttpClient<Comparison.Gateway.Clients.MicroservicesArchitectureClient>()
                        .ConfigurePrimaryHttpMessageHandler(() => new FakeArchitectureHandler());

                    services.AddHttpClient<Comparison.Gateway.Clients.VirtualActorsArchitectureClient>()
                        .ConfigurePrimaryHttpMessageHandler(() => new FakeArchitectureHandler());
                });
            });
    }

    private sealed class FakeArchitectureHandler : HttpMessageHandler {
        private readonly Dictionary<string, int> _inventory = new(StringComparer.OrdinalIgnoreCase);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (string.Equals(path, $"/api/scenarios/reset", StringComparison.OrdinalIgnoreCase)) {
                var reset = request.Content!.ReadFromJsonAsync<ResetInventoryRequest>(cancellationToken).GetAwaiter().GetResult()!;
                _inventory[reset.ProductId] = reset.Quantity;
                return Json(new InventoryResponse(reset.ProductId, reset.Quantity));
            }

            if (path.StartsWith($"/api/inventory/", StringComparison.OrdinalIgnoreCase)) {
                var productId = path.Split('/').Last();
                return Json(new InventoryResponse(productId, _inventory.GetValueOrDefault(productId)));
            }

            if (string.Equals(path, $"/api/orders", StringComparison.OrdinalIgnoreCase)) {
                var order = request.Content!.ReadFromJsonAsync<RunScenarioRequest>(cancellationToken).GetAwaiter().GetResult()!;
                var available = _inventory.GetValueOrDefault(order.ProductId);
                if (available < order.Quantity) {
                    return Json(new OrderResponse(order.OrderId, OrderStatus.Rejected, $"InsufficientInventory"));
                }

                if (order.SimulatePaymentFailure) {
                    return Json(new OrderResponse(order.OrderId, OrderStatus.Rejected, $"PaymentFailed"));
                }

                _inventory[order.ProductId] = available - order.Quantity;
                return Json(new OrderResponse(order.OrderId, OrderStatus.Completed, null));
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


