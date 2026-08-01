namespace Microservices.Tests;

using Comparison.Contracts;
using FluentAssertions;
using Microservices.Tests.Infrastructure;
using System.Net.Http.Json;
using Xunit;

/// <summary>
/// Tests for the microservice-style order workflow through Orders.Api.
/// </summary>
public sealed class OrdersApiWorkflowTests {
    [Fact]
    public async Task OrdersApi_Should_CompleteSuccessfulOrder() {
        using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();
        await factory.InventoryClient.ResetAsync(new ResetInventoryRequest("product-001", 10), CancellationToken.None);

        var response = await PlaceOrderAsync(client, new RunScenarioRequest { ProductId = "product-001", Quantity = 2 });
        var inventory = await factory.InventoryClient.GetAsync("product-001", CancellationToken.None);

        response.Status.Should().Be(OrderStatus.Completed);
        inventory.AvailableQuantity.Should().Be(8);
    }

    [Fact]
    public async Task OrdersApi_Should_RejectOrderWhenInventoryIsInsufficient() {
        using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();
        await factory.InventoryClient.ResetAsync(new ResetInventoryRequest("product-001", 1), CancellationToken.None);

        var response = await PlaceOrderAsync(client, new RunScenarioRequest { ProductId = "product-001", Quantity = 2 });
        var inventory = await factory.InventoryClient.GetAsync("product-001", CancellationToken.None);

        response.Status.Should().Be(OrderStatus.Rejected);
        response.Reason.Should().Be("InsufficientInventory");
        inventory.AvailableQuantity.Should().Be(1);
    }

    [Fact]
    public async Task OrdersApi_Should_ReleaseInventoryWhenPaymentFails() {
        using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();
        await factory.InventoryClient.ResetAsync(new ResetInventoryRequest("product-001", 10), CancellationToken.None);

        var response = await PlaceOrderAsync(client, new RunScenarioRequest {
            ProductId = "product-001",
            Quantity = 2,
            SimulatePaymentFailure = true
        });
        var inventory = await factory.InventoryClient.GetAsync("product-001", CancellationToken.None);

        response.Status.Should().Be(OrderStatus.Rejected);
        response.Reason.Should().Be("PaymentFailed");
        inventory.AvailableQuantity.Should().Be(10);
    }

    [Fact]
    public async Task OrdersApi_Should_NotOverReserveInventoryForConcurrentOrders() {
        using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();
        await factory.InventoryClient.ResetAsync(new ResetInventoryRequest("product-001", 3), CancellationToken.None);

        var tasks = Enumerable.Range(1, 10)
            .Select(index => PlaceOrderAsync(client, new RunScenarioRequest {
                OrderId = Guid.NewGuid(),
                ProductId = "product-001",
                Quantity = 1,
                IdempotencyKey = $"concurrent-{index}"
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        var inventory = await factory.InventoryClient.GetAsync("product-001", CancellationToken.None);

        results.Count(result => result.Status == OrderStatus.Completed).Should().Be(3);
        results.Count(result => result.Status == OrderStatus.Rejected).Should().Be(7);
        inventory.AvailableQuantity.Should().Be(0);
    }

    [Fact]
    public async Task OrdersApi_Should_ReturnExistingOrderForDuplicateIdempotencyKey() {
        using var factory = new OrdersApiFactory();
        using var client = factory.CreateClient();
        await factory.InventoryClient.ResetAsync(new ResetInventoryRequest("product-001", 10), CancellationToken.None);

        var orderId = Guid.NewGuid();
        var request = new RunScenarioRequest {
            OrderId = orderId,
            ProductId = "product-001",
            Quantity = 2,
            IdempotencyKey = "duplicate-request"
        };

        var first = await PlaceOrderAsync(client, request);
        var second = await PlaceOrderAsync(client, request);
        var inventory = await factory.InventoryClient.GetAsync("product-001", CancellationToken.None);

        first.Should().Be(second);
        inventory.AvailableQuantity.Should().Be(8);
    }

    private static async Task<OrderResponse> PlaceOrderAsync(HttpClient client, RunScenarioRequest request) {
        var response = await client.PostAsJsonAsync("/api/orders", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
    }
}


