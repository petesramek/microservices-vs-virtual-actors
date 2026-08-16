namespace VirtualActors.Tests;

using Ordering.Grains.Contracts;
using Ordering.Grains.Grains.Abstraction;
using Shouldly;
using Workbench.Contracts.Orders;
using Xunit;

/// <summary>
/// Tests for the virtual actor-style order workflow.
/// </summary>
[Collection(OrleansClusterFixtureDefinition.Name)]
public sealed class VirtualActorOrderWorkflowTests {
    private readonly OrleansClusterFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualActorOrderWorkflowTests"/> class.
    /// </summary>
    /// <param name="fixture">The Orleans cluster fixture.</param>
    public VirtualActorOrderWorkflowTests(OrleansClusterFixture fixture) {
        _fixture = fixture;
    }

    [Fact]
    public async Task OrdersApiCompletesSuccessfulOrder() {
        var productId = UniqueProductId();
        await ResetInventoryAsync(productId, 10);

        GrainOrderResult result = await PlaceOrderAsync(productId, quantity: 2, simulatePaymentFailure: false);
        InventorySnapshot inventory = await GetInventoryAsync(productId);

        result.Status.ShouldBe(OrderStatus.Completed.ToString());
        inventory.AvailableQuantity.ShouldBe(8);
    }

    [Fact]
    public async Task OrdersApiRejectsOrderWhenInventoryIsInsufficient() {
        var productId = UniqueProductId();
        await ResetInventoryAsync(productId, 1);

        GrainOrderResult result = await PlaceOrderAsync(productId, quantity: 2, simulatePaymentFailure: false);
        InventorySnapshot inventory = await GetInventoryAsync(productId);

        result.Status.ShouldBe(OrderStatus.Rejected.ToString());
        result.Reason.ShouldBe($"InsufficientInventory");
        inventory.AvailableQuantity.ShouldBe(1);
    }

    [Fact]
    public async Task OrdersApiReleasesInventoryWhenPaymentFails() {
        var productId = UniqueProductId();
        await ResetInventoryAsync(productId, 10);

        GrainOrderResult result = await PlaceOrderAsync(productId, quantity: 2, simulatePaymentFailure: true);
        InventorySnapshot inventory = await GetInventoryAsync(productId);

        result.Status.ShouldBe(OrderStatus.Rejected.ToString());
        result.Reason.ShouldBe($"PaymentFailed");
        inventory.AvailableQuantity.ShouldBe(10);
    }

    [Fact]
    public async Task OrdersApiDoesNotOverReserveInventoryForConcurrentOrders() {
        var productId = UniqueProductId();
        await ResetInventoryAsync(productId, 3);

        Task<GrainOrderResult>[] tasks = Enumerable.Range(1, 10)
            .Select(index => PlaceOrderAsync(productId, quantity: 1, simulatePaymentFailure: false, idempotencyKey: $"concurrent-{Guid.NewGuid():N}-{index}"))
            .ToArray();

        GrainOrderResult[] results = await Task.WhenAll(tasks);
        InventorySnapshot inventory = await GetInventoryAsync(productId);

        results.Count(result => string.Equals(result.Status, OrderStatus.Completed.ToString(), StringComparison.Ordinal)).ShouldBe(3);
        results.Count(result => string.Equals(result.Status, OrderStatus.Rejected.ToString(), StringComparison.Ordinal)).ShouldBe(7);
        inventory.AvailableQuantity.ShouldBe(0);
    }

    [Fact]
    public async Task OrderGrainReturnsExistingResultForDuplicateOrder() {
        var productId = UniqueProductId();
        await ResetInventoryAsync(productId, 10);
        var orderId = Guid.NewGuid();
        const string idempotencyKey = $"duplicate-grain-call";

        IOrderGrain order = _fixture.Cluster.Client.GetGrain<IOrderGrain>(orderId);
        GrainOrderResult first = await order.PlaceAsync(idempotencyKey, $"customer-001", productId, 2, simulatePaymentFailure: false);
        GrainOrderResult second = await order.PlaceAsync(idempotencyKey, $"customer-001", productId, 2, simulatePaymentFailure: false);
        InventorySnapshot inventory = await GetInventoryAsync(productId);

        first.ShouldBe(second);
        inventory.AvailableQuantity.ShouldBe(8);
    }

    private async Task ResetInventoryAsync(string productId, int quantity) {
        IInventoryItemGrain inventory = _fixture.Cluster.Client.GetGrain<IInventoryItemGrain>(productId);
        await inventory.ResetAsync(quantity).ConfigureAwait(false);
    }

    private async Task<Ordering.Grains.Contracts.InventorySnapshot> GetInventoryAsync(string productId) {
        IInventoryItemGrain inventory = _fixture.Cluster.Client.GetGrain<IInventoryItemGrain>(productId);
        return await inventory.GetAsync().ConfigureAwait(false);
    }

    private async Task<Ordering.Grains.Contracts.GrainOrderResult> PlaceOrderAsync(
        string productId,
        int quantity,
        bool simulatePaymentFailure,
        string? idempotencyKey = null) {
        IOrderGrain order = _fixture.Cluster.Client.GetGrain<IOrderGrain>(Guid.NewGuid());
        return await order.PlaceAsync(
            idempotencyKey ?? Guid.NewGuid().ToString($"N"),
$"customer-001",
            productId,
            quantity,
            simulatePaymentFailure).ConfigureAwait(false);
    }

    private static string UniqueProductId() {
        return $"product-{Guid.NewGuid():N}";
    }
}


