using ArchitectureComparison.Contracts;
using FluentAssertions;
using Ordering.Grains.Interfaces;
using Xunit;

namespace VirtualActors.Tests;

/// <summary>
/// Tests for the virtual actor-style order workflow.
/// </summary>
[Collection(OrleansClusterCollection.Name)]
public sealed class VirtualActorWorkflowTests
{
    private readonly OrleansClusterFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualActorWorkflowTests"/> class.
    /// </summary>
    /// <param name="fixture">The Orleans cluster fixture.</param>
    public VirtualActorWorkflowTests(OrleansClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Successful_order_completes()
    {
        var productId = UniqueProductId();
        await ResetInventoryAsync(productId, 10);

        var result = await PlaceOrderAsync(productId, quantity: 2, simulatePaymentFailure: false);
        var inventory = await GetInventoryAsync(productId);

        result.Status.Should().Be(OrderStatus.Completed.ToString());
        inventory.AvailableQuantity.Should().Be(8);
    }

    [Fact]
    public async Task Insufficient_inventory_rejects_order()
    {
        var productId = UniqueProductId();
        await ResetInventoryAsync(productId, 1);

        var result = await PlaceOrderAsync(productId, quantity: 2, simulatePaymentFailure: false);
        var inventory = await GetInventoryAsync(productId);

        result.Status.Should().Be(OrderStatus.Rejected.ToString());
        result.Reason.Should().Be("InsufficientInventory");
        inventory.AvailableQuantity.Should().Be(1);
    }

    [Fact]
    public async Task Payment_failure_releases_inventory()
    {
        var productId = UniqueProductId();
        await ResetInventoryAsync(productId, 10);

        var result = await PlaceOrderAsync(productId, quantity: 2, simulatePaymentFailure: true);
        var inventory = await GetInventoryAsync(productId);

        result.Status.Should().Be(OrderStatus.Rejected.ToString());
        result.Reason.Should().Be("PaymentFailed");
        inventory.AvailableQuantity.Should().Be(10);
    }

    [Fact]
    public async Task Concurrent_orders_do_not_overreserve_inventory()
    {
        var productId = UniqueProductId();
        await ResetInventoryAsync(productId, 3);

        var tasks = Enumerable.Range(1, 10)
            .Select(index => PlaceOrderAsync(productId, quantity: 1, simulatePaymentFailure: false, idempotencyKey: $"concurrent-{Guid.NewGuid():N}-{index}"))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        var inventory = await GetInventoryAsync(productId);

        results.Count(result => result.Status == OrderStatus.Completed.ToString()).Should().Be(3);
        results.Count(result => result.Status == OrderStatus.Rejected.ToString()).Should().Be(7);
        inventory.AvailableQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Duplicate_order_grain_call_is_idempotent()
    {
        var productId = UniqueProductId();
        await ResetInventoryAsync(productId, 10);
        var orderId = Guid.NewGuid();
        const string idempotencyKey = "duplicate-grain-call";

        var order = _fixture.Cluster.Client.GetGrain<IOrderGrain>(orderId);
        var first = await order.PlaceAsync(idempotencyKey, "customer-001", productId, 2, simulatePaymentFailure: false);
        var second = await order.PlaceAsync(idempotencyKey, "customer-001", productId, 2, simulatePaymentFailure: false);
        var inventory = await GetInventoryAsync(productId);

        first.Should().Be(second);
        inventory.AvailableQuantity.Should().Be(8);
    }

    private async Task ResetInventoryAsync(string productId, int quantity)
    {
        var inventory = _fixture.Cluster.Client.GetGrain<IInventoryItemGrain>(productId);
        await inventory.ResetAsync(quantity);
    }

    private async Task<Ordering.Grains.Contracts.InventorySnapshot> GetInventoryAsync(string productId)
    {
        var inventory = _fixture.Cluster.Client.GetGrain<IInventoryItemGrain>(productId);
        return await inventory.GetAsync();
    }

    private async Task<Ordering.Grains.Contracts.GrainOrderResult> PlaceOrderAsync(
        string productId,
        int quantity,
        bool simulatePaymentFailure,
        string? idempotencyKey = null)
    {
        var order = _fixture.Cluster.Client.GetGrain<IOrderGrain>(Guid.NewGuid());
        return await order.PlaceAsync(
            idempotencyKey ?? Guid.NewGuid().ToString("N"),
            "customer-001",
            productId,
            quantity,
            simulatePaymentFailure);
    }

    private static string UniqueProductId()
    {
        return $"product-{Guid.NewGuid():N}";
    }
}
