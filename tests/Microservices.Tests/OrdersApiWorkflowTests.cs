namespace Microservices.Tests;

using Comparison.Contracts;
using Microservices.Tests.Infrastructure;
using Shouldly;
using System.Net.Http.Json;
using Xunit;

/// <summary>
/// Tests for the microservice-style order workflow through Orders.Api.
/// </summary>
public sealed class OrdersApiWorkflowTests {
    [Fact]
    public async Task OrdersApiCompletesSuccessfulOrder() {
        using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();
        await factory.InventoryClient.ResetAsync(new ResetInventoryRequest("product-001", 10), CancellationToken.None);

        var response = await PlaceOrderAsync(client, new RunScenarioRequest { ProductId = "product-001", Quantity = 2 });
        var inventory = await factory.InventoryClient.GetAsync("product-001", CancellationToken.None);

        response.Status.ShouldBe(OrderStatus.Completed);
        inventory.AvailableQuantity.ShouldBe(8);
    }

    [Fact]
    public async Task OrdersApiRejectsOrderWhenInventoryIsInsufficient() {
        using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();
        await factory.InventoryClient.ResetAsync(new ResetInventoryRequest("product-001", 1), CancellationToken.None);

        var response = await PlaceOrderAsync(client, new RunScenarioRequest { ProductId = "product-001", Quantity = 2 });
        var inventory = await factory.InventoryClient.GetAsync("product-001", CancellationToken.None);

        response.Status.ShouldBe(OrderStatus.Rejected);
        response.Reason.ShouldBe("InsufficientInventory");
        inventory.AvailableQuantity.ShouldBe(1);
    }

    [Fact]
    public async Task OrdersApiReleasesInventoryWhenPaymentFails() {
        using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();
        await factory.InventoryClient.ResetAsync(new ResetInventoryRequest("product-001", 10), CancellationToken.None);

        var response = await PlaceOrderAsync(client, new RunScenarioRequest {
            ProductId = "product-001",
            Quantity = 2,
            SimulatePaymentFailure = true,
        });
        var inventory = await factory.InventoryClient.GetAsync("product-001", CancellationToken.None);

        response.Status.ShouldBe(OrderStatus.Rejected);
        response.Reason.ShouldBe("PaymentFailed");
        inventory.AvailableQuantity.ShouldBe(10);
    }

    [Fact]
    public async Task OrdersApiDoesNotOverReserveInventoryForConcurrentOrders() {
        using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();
        await factory.InventoryClient.ResetAsync(new ResetInventoryRequest("product-001", 3), CancellationToken.None);

        var tasks = Enumerable.Range(1, 10)
            .Select(index => PlaceOrderAsync(client, new RunScenarioRequest {
                OrderId = Guid.NewGuid(),
                ProductId = "product-001",
                Quantity = 1,
                IdempotencyKey = $"concurrent-{index}",
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        var inventory = await factory.InventoryClient.GetAsync("product-001", CancellationToken.None);

        results.Count(result => result.Status == OrderStatus.Completed).ShouldBe(3);
        results.Count(result => result.Status == OrderStatus.Rejected).ShouldBe(7);
        inventory.AvailableQuantity.ShouldBe(0);
    }

    [Fact]
    public async Task OrdersApiReturnsExistingOrderForDuplicateIdempotencyKey() {
        using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();
        await factory.InventoryClient.ResetAsync(new ResetInventoryRequest("product-001", 10), CancellationToken.None);

        var orderId = Guid.NewGuid();
        var request = new RunScenarioRequest {
            OrderId = orderId,
            ProductId = "product-001",
            Quantity = 2,
            IdempotencyKey = "duplicate-request",
        };

        var first = await PlaceOrderAsync(client, request);
        var second = await PlaceOrderAsync(client, request);
        var inventory = await factory.InventoryClient.GetAsync("product-001", CancellationToken.None);

        first.ShouldBe(second);
        inventory.AvailableQuantity.ShouldBe(8);
    }

    private static async Task<OrderResponse> PlaceOrderAsync(HttpClient client, RunScenarioRequest request) {
        var response = await client.PostAsJsonAsync("/api/orders", request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>().ConfigureAwait(false))!;
    }
}


