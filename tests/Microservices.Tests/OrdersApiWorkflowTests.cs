namespace Microservices.Tests;

using Microservices.Tests.Infrastructure;
using Shouldly;
using System.Net.Http.Json;
using Workbench.Contracts;
using Xunit;

/// <summary>
/// Tests for the microservice-style order workflow through Orders.Api.
/// </summary>
public sealed class OrdersApiWorkflowTests {
    [Fact]
    public async Task OrdersApiCompletesSuccessfulOrder() {
        await using var factory = new OrdersApiFactory();
        using HttpClient client = factory.CreateClient();
        await factory.InventoryClient.ResetAsync(new ResetInventoryRequest($"product-001", 10), CancellationToken.None);

        OrderResponse response = await PlaceOrderAsync(client, new RunScenarioRequest { ProductId = $"product-001", Quantity = 2 });
        InventoryResponse inventory = await factory.InventoryClient.GetAsync($"product-001", CancellationToken.None);

        response.Status.ShouldBe(OrderStatus.Completed);
        inventory.AvailableQuantity.ShouldBe(8);
    }

    [Fact]
    public async Task OrdersApiRejectsOrderWhenInventoryIsInsufficient() {
        await using var factory = new OrdersApiFactory();
        using HttpClient client = factory.CreateClient();
        await factory.InventoryClient.ResetAsync(new ResetInventoryRequest($"product-001", 1), CancellationToken.None);

        OrderResponse response = await PlaceOrderAsync(client, new RunScenarioRequest { ProductId = $"product-001", Quantity = 2 });
        InventoryResponse inventory = await factory.InventoryClient.GetAsync($"product-001", CancellationToken.None);

        response.Status.ShouldBe(OrderStatus.Rejected);
        response.Reason.ShouldBe($"InsufficientInventory");
        inventory.AvailableQuantity.ShouldBe(1);
    }

    [Fact]
    public async Task OrdersApiReleasesInventoryWhenPaymentFails() {
        await using var factory = new OrdersApiFactory();
        using HttpClient client = factory.CreateClient();
        await factory.InventoryClient.ResetAsync(new ResetInventoryRequest($"product-001", 10), CancellationToken.None);

        OrderResponse response = await PlaceOrderAsync(client, new RunScenarioRequest {
            ProductId = $"product-001",
            Quantity = 2,
            SimulatePaymentFailure = true,
        });
        InventoryResponse inventory = await factory.InventoryClient.GetAsync($"product-001", CancellationToken.None);

        response.Status.ShouldBe(OrderStatus.Rejected);
        response.Reason.ShouldBe($"PaymentFailed");
        inventory.AvailableQuantity.ShouldBe(10);
    }

    [Fact]
    public async Task OrdersApiDoesNotOverReserveInventoryForConcurrentOrders() {
        await using var factory = new OrdersApiFactory();
        using HttpClient client = factory.CreateClient();
        await factory.InventoryClient.ResetAsync(new ResetInventoryRequest($"product-001", 3), CancellationToken.None);

        Task<OrderResponse>[] tasks = Enumerable.Range(1, 10)
            .Select(index => PlaceOrderAsync(client, new RunScenarioRequest {
                OrderId = Guid.NewGuid(),
                ProductId = $"product-001",
                Quantity = 1,
                IdempotencyKey = $"concurrent-{index}",
            }))
            .ToArray();

        OrderResponse[] results = await Task.WhenAll(tasks);
        InventoryResponse inventory = await factory.InventoryClient.GetAsync($"product-001", CancellationToken.None);

        results.Count(result => result.Status == OrderStatus.Completed).ShouldBe(3);
        results.Count(result => result.Status == OrderStatus.Rejected).ShouldBe(7);
        inventory.AvailableQuantity.ShouldBe(0);
    }

    [Fact]
    public async Task OrdersApiReturnsExistingOrderForDuplicateIdempotencyKey() {
        await using var factory = new OrdersApiFactory();
        using HttpClient client = factory.CreateClient();
        await factory.InventoryClient.ResetAsync(new ResetInventoryRequest($"product-001", 10), CancellationToken.None);

        var orderId = Guid.NewGuid();
        var request = new RunScenarioRequest {
            OrderId = orderId,
            ProductId = $"product-001",
            Quantity = 2,
            IdempotencyKey = $"duplicate-request",
        };

        OrderResponse first = await PlaceOrderAsync(client, request);
        OrderResponse second = await PlaceOrderAsync(client, request);
        InventoryResponse inventory = await factory.InventoryClient.GetAsync($"product-001", CancellationToken.None);

        first.ShouldBe(second);
        inventory.AvailableQuantity.ShouldBe(8);
    }

    private static async Task<OrderResponse> PlaceOrderAsync(HttpClient client, RunScenarioRequest request) {
        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/orders", request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>().ConfigureAwait(false))!;
    }
}


