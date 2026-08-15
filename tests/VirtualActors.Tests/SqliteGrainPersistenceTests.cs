namespace VirtualActors.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Grains.Contracts;
using Ordering.Grains.Grains.Abstraction;
using Ordering.Grains.State;
using Ordering.Persistence.Sqlite.Extensions;
using Orleans;
using Orleans.Runtime;
using Orleans.Storage;
using Orleans.TestingHost;
using Shouldly;
using System.Globalization;
using Workbench.Contracts;
using Xunit;

/// <summary>
/// Verifies SQLite-backed grain persistence.
/// </summary>
public sealed class SqliteGrainPersistenceTests {
    private const string InventoryStateName = "inventory";
    private const string SecondaryStorageProviderName = "SecondaryOrderingStorage";
    private const string StorageProviderName = "OrderingStorage";

    /// <summary>
    /// Verifies that reading missing inventory state returns its default value.
    /// </summary>
    [Fact]
    public async Task MissingInventoryStateReturnsDefaultValue() {
        await using PersistenceTestContext context = await CreateContextAsync();

        IInventoryItemGrain inventory = context.Cluster.Client
            .GetGrain<IInventoryItemGrain>(context.CreateProductId());

        int availableQuantity = (await inventory.GetAsync()).AvailableQuantity;

        availableQuantity.ShouldBe(0);
    }

    /// <summary>
    /// Verifies that writing previously missing inventory state inserts a new record.
    /// </summary>
    [Fact]
    public async Task WritingMissingInventoryStateInsertsNewRecord() {
        await using PersistenceTestContext context = await CreateContextAsync();
        string productId = context.CreateProductId();

        IInventoryItemGrain inventory = context.Cluster.Client
            .GetGrain<IInventoryItemGrain>(productId);

        await inventory.ResetAsync(10);

        long recordCount = await CountInventoryRecordsAsync(context, productId);
        long version = await ReadInventoryVersionAsync(context, productId);

        recordCount.ShouldBe(1);
        version.ShouldBe(1);
    }

    /// <summary>
    /// Verifies that writing existing inventory state updates its persisted record.
    /// </summary>
    [Fact]
    public async Task UpdatingExistingInventoryStateUpdatesRecord() {
        await using PersistenceTestContext context = await CreateContextAsync();
        string productId = context.CreateProductId();

        IInventoryItemGrain inventory = context.Cluster.Client
            .GetGrain<IInventoryItemGrain>(productId);

        await inventory.ResetAsync(10);
        await inventory.ResetAsync(15);

        int availableQuantity = (await inventory.GetAsync()).AvailableQuantity;
        long recordCount = await CountInventoryRecordsAsync(context, productId);
        long version = await ReadInventoryVersionAsync(context, productId);

        availableQuantity.ShouldBe(15);
        recordCount.ShouldBe(1);
        version.ShouldBe(2);
    }

    /// <summary>
    /// Verifies that stale persisted versions reject subsequent grain-state writes.
    /// </summary>
    [Fact]
    public async Task StaleVersionRejectsWrite() {
        await using PersistenceTestContext context = await CreateContextAsync();
        string productId = context.CreateProductId();

        IInventoryItemGrain inventory = context.Cluster.Client
            .GetGrain<IInventoryItemGrain>(productId);

        await inventory.ResetAsync(10);
        await SetInventoryVersionAsync(context, productId, version: 2);

        Exception exception = await Should.ThrowAsync<Exception>(
            async () => await inventory.ResetAsync(15));

        exception.ToString().ShouldContain("state");
        (await inventory.GetAsync()).AvailableQuantity.ShouldBe(10);
        (await ReadInventoryVersionAsync(context, productId)).ShouldBe(2);
    }

    /// <summary>
    /// Verifies that separate grain identities persist isolated state records.
    /// </summary>
    [Fact]
    public async Task DifferentGrainIdsPersistIsolatedState() {
        await using PersistenceTestContext context = await CreateContextAsync();
        string firstProductId = context.CreateProductId();
        string secondProductId = context.CreateProductId();

        IInventoryItemGrain firstInventory = context.Cluster.Client
            .GetGrain<IInventoryItemGrain>(firstProductId);
        IInventoryItemGrain secondInventory = context.Cluster.Client
            .GetGrain<IInventoryItemGrain>(secondProductId);

        await firstInventory.ResetAsync(10);
        await secondInventory.ResetAsync(20);

        (await firstInventory.GetAsync()).AvailableQuantity.ShouldBe(10);
        (await secondInventory.GetAsync()).AvailableQuantity.ShouldBe(20);
        (await CountInventoryRecordsAsync(context, firstProductId)).ShouldBe(1);
        (await CountInventoryRecordsAsync(context, secondProductId)).ShouldBe(1);
    }

    /// <summary>
    /// Verifies that persisted payment idempotency state survives a cluster restart.
    /// </summary>
    [Fact]
    public async Task PaymentStateSurvivesClusterRestart() {
        string databasePath = CreateDatabasePath();
        string connectionString = $"Data Source={databasePath}";
        string customerId = CreateIdentifier("customer");
        string idempotencyKey = CreateIdentifier("payment");
        string serviceId = CreateIdentifier("ordering-persistence");
        string clusterId = CreateIdentifier("ordering-persistence");

        try {
            InProcessTestCluster firstCluster = await StartClusterAsync(
                connectionString,
                serviceId,
                clusterId);

            try {
                IPaymentAccountGrain payment = firstCluster.Client
                    .GetGrain<IPaymentAccountGrain>(customerId);

                PaymentAuthorizationResult result = await payment.AuthorizeAsync(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    idempotencyKey,
                    simulateFailure: false);

                result.Authorized.ShouldBeTrue();
            } finally {
                await firstCluster.DisposeAsync();
            }

            InProcessTestCluster secondCluster = await StartClusterAsync(
                connectionString,
                serviceId,
                clusterId);

            try {
                IPaymentAccountGrain payment = secondCluster.Client
                    .GetGrain<IPaymentAccountGrain>(customerId);

                PaymentAuthorizationResult result = await payment.AuthorizeAsync(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    idempotencyKey,
                    simulateFailure: true);

                result.Authorized.ShouldBeTrue();
                result.Reason.ShouldBeNull();
            } finally {
                await secondCluster.DisposeAsync();
            }
        } finally {
            DeleteDatabaseFiles(databasePath);
        }
    }

    /// <summary>
    /// Verifies that a persisted final order result survives a cluster restart.
    /// </summary>
    [Fact]
    public async Task OrderStateSurvivesClusterRestart() {
        string databasePath = CreateDatabasePath();
        string connectionString = $"Data Source={databasePath}";
        Guid orderId = Guid.NewGuid();
        string productId = CreateIdentifier("product");
        string customerId = CreateIdentifier("customer");
        string idempotencyKey = CreateIdentifier("order");
        string serviceId = CreateIdentifier("ordering-persistence");
        string clusterId = CreateIdentifier("ordering-persistence");

        try {
            InProcessTestCluster firstCluster = await StartClusterAsync(
                connectionString,
                serviceId,
                clusterId);

            GrainOrderResult firstResult;

            try {
                IInventoryItemGrain inventory = firstCluster.Client
                    .GetGrain<IInventoryItemGrain>(productId);
                IOrderGrain order = firstCluster.Client
                    .GetGrain<IOrderGrain>(orderId);

                await inventory.ResetAsync(10);
                firstResult = await order.PlaceAsync(
                    idempotencyKey,
                    customerId,
                    productId,
                    quantity: 3,
                    simulatePaymentFailure: false);

                firstResult.Status.ShouldBe(OrderStatus.Completed.ToString());
            } finally {
                await firstCluster.DisposeAsync();
            }

            InProcessTestCluster secondCluster = await StartClusterAsync(
                connectionString,
                serviceId,
                clusterId);

            try {
                IOrderGrain order = secondCluster.Client
                    .GetGrain<IOrderGrain>(orderId);

                GrainOrderResult? restoredResult = await order.GetAsync();
                GrainOrderResult repeatedResult = await order.PlaceAsync(
                    idempotencyKey,
                    customerId,
                    productId,
                    quantity: 3,
                    simulatePaymentFailure: true);

                restoredResult.ShouldNotBeNull();
                restoredResult.ShouldBe(firstResult);
                repeatedResult.ShouldBe(firstResult);
            } finally {
                await secondCluster.DisposeAsync();
            }
        } finally {
            DeleteDatabaseFiles(databasePath);
        }
    }

    /// <summary>
    /// Verifies that a persisted successful payment remains authoritative for a new order using the same key.
    /// </summary>
    [Fact]
    public async Task SuccessfulPaymentRemainsAuthoritativeForNewOrder() {
        await using PersistenceTestContext context = await CreateContextAsync();
        string productId = context.CreateProductId();
        string customerId = CreateIdentifier("customer");
        string idempotencyKey = CreateIdentifier("payment");

        IInventoryItemGrain inventory = context.Cluster.Client
            .GetGrain<IInventoryItemGrain>(productId);
        IOrderGrain firstOrder = context.Cluster.Client
            .GetGrain<IOrderGrain>(Guid.NewGuid());
        IOrderGrain secondOrder = context.Cluster.Client
            .GetGrain<IOrderGrain>(Guid.NewGuid());

        await inventory.ResetAsync(10);
        GrainOrderResult firstResult = await firstOrder.PlaceAsync(
            idempotencyKey,
            customerId,
            productId,
            quantity: 2,
            simulatePaymentFailure: false);

        await inventory.ResetAsync(10);
        GrainOrderResult secondResult = await secondOrder.PlaceAsync(
            idempotencyKey,
            customerId,
            productId,
            quantity: 2,
            simulatePaymentFailure: true);
        InventorySnapshot inventoryAfterSecondOrder = await inventory.GetAsync();

        firstResult.Status.ShouldBe(OrderStatus.Completed.ToString());
        secondResult.Status.ShouldBe(OrderStatus.Completed.ToString());
        secondResult.Reason.ShouldBeNull();
        inventoryAfterSecondOrder.AvailableQuantity.ShouldBe(8);
    }

    /// <summary>
    /// Verifies that a persisted failed payment remains authoritative for a new order using the same key.
    /// </summary>
    [Fact]
    public async Task FailedPaymentRemainsAuthoritativeForNewOrder() {
        await using PersistenceTestContext context = await CreateContextAsync();
        string productId = context.CreateProductId();
        string customerId = CreateIdentifier("customer");
        string idempotencyKey = CreateIdentifier("payment");

        IInventoryItemGrain inventory = context.Cluster.Client
            .GetGrain<IInventoryItemGrain>(productId);
        IOrderGrain firstOrder = context.Cluster.Client
            .GetGrain<IOrderGrain>(Guid.NewGuid());
        IOrderGrain secondOrder = context.Cluster.Client
            .GetGrain<IOrderGrain>(Guid.NewGuid());

        await inventory.ResetAsync(10);
        GrainOrderResult firstResult = await firstOrder.PlaceAsync(
            idempotencyKey,
            customerId,
            productId,
            quantity: 2,
            simulatePaymentFailure: true);

        await inventory.ResetAsync(10);
        GrainOrderResult secondResult = await secondOrder.PlaceAsync(
            idempotencyKey,
            customerId,
            productId,
            quantity: 2,
            simulatePaymentFailure: false);
        InventorySnapshot inventoryAfterSecondOrder = await inventory.GetAsync();

        firstResult.Status.ShouldBe(OrderStatus.Rejected.ToString());
        firstResult.Reason.ShouldBe("PaymentFailed");
        secondResult.Status.ShouldBe(OrderStatus.Rejected.ToString());
        secondResult.Reason.ShouldBe("PaymentFailed");
        inventoryAfterSecondOrder.AvailableQuantity.ShouldBe(10);
    }

    /// <summary>
    /// Verifies that identical idempotency keys remain isolated across customer grains.
    /// </summary>
    [Fact]
    public async Task SamePaymentKeyAcrossCustomersRemainsIsolated() {
        await using PersistenceTestContext context = await CreateContextAsync();
        string productId = context.CreateProductId();
        string firstCustomerId = CreateIdentifier("customer");
        string secondCustomerId = CreateIdentifier("customer");
        string idempotencyKey = CreateIdentifier("payment");

        IInventoryItemGrain inventory = context.Cluster.Client
            .GetGrain<IInventoryItemGrain>(productId);
        IOrderGrain firstOrder = context.Cluster.Client
            .GetGrain<IOrderGrain>(Guid.NewGuid());
        IOrderGrain secondOrder = context.Cluster.Client
            .GetGrain<IOrderGrain>(Guid.NewGuid());

        await inventory.ResetAsync(10);
        GrainOrderResult firstResult = await firstOrder.PlaceAsync(
            idempotencyKey,
            firstCustomerId,
            productId,
            quantity: 2,
            simulatePaymentFailure: false);

        await inventory.ResetAsync(10);
        GrainOrderResult secondResult = await secondOrder.PlaceAsync(
            idempotencyKey,
            secondCustomerId,
            productId,
            quantity: 2,
            simulatePaymentFailure: true);
        InventorySnapshot inventoryAfterSecondOrder = await inventory.GetAsync();

        firstResult.Status.ShouldBe(OrderStatus.Completed.ToString());
        secondResult.Status.ShouldBe(OrderStatus.Rejected.ToString());
        secondResult.Reason.ShouldBe("PaymentFailed");
        inventoryAfterSecondOrder.AvailableQuantity.ShouldBe(10);
    }

    /// <summary>
    /// Verifies that repeated duplicate-request runs reserve inventory once per new order identity.
    /// </summary>
    [Fact]
    public async Task RepeatedDuplicateRequestRunReservesInventoryOncePerOrder() {
        await using PersistenceTestContext context = await CreateContextAsync();
        string productId = context.CreateProductId();
        string customerId = CreateIdentifier("customer");
        string idempotencyKey = CreateIdentifier("payment");

        IInventoryItemGrain inventory = context.Cluster.Client
            .GetGrain<IInventoryItemGrain>(productId);
        IOrderGrain firstOrder = context.Cluster.Client
            .GetGrain<IOrderGrain>(Guid.NewGuid());
        IOrderGrain secondOrder = context.Cluster.Client
            .GetGrain<IOrderGrain>(Guid.NewGuid());

        await inventory.ResetAsync(10);
        GrainOrderResult[] firstResponses = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => firstOrder.PlaceAsync(
                idempotencyKey,
                customerId,
                productId,
                quantity: 2,
                simulatePaymentFailure: false)));
        InventorySnapshot inventoryAfterFirstRun = await inventory.GetAsync();

        await inventory.ResetAsync(10);
        GrainOrderResult[] secondResponses = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => secondOrder.PlaceAsync(
                idempotencyKey,
                customerId,
                productId,
                quantity: 2,
                simulatePaymentFailure: true)));
        InventorySnapshot inventoryAfterSecondRun = await inventory.GetAsync();

        firstResponses.ShouldAllBe(result =>
            result.Status == OrderStatus.Completed.ToString());
        firstResponses.Select(result => result.OrderId).Distinct().Count().ShouldBe(1);
        inventoryAfterFirstRun.AvailableQuantity.ShouldBe(8);
        secondResponses.ShouldAllBe(result =>
            result.Status == OrderStatus.Completed.ToString());
        secondResponses.Select(result => result.OrderId).Distinct().Count().ShouldBe(1);
        inventoryAfterSecondRun.AvailableQuantity.ShouldBe(8);
    }

    /// <summary>
    /// Verifies that persisted payment idempotency remains authoritative for a new order after restart.
    /// </summary>
    [Fact]
    public async Task RepeatedOrderAfterRestartReusesPersistedPaymentResult() {
        string databasePath = CreateDatabasePath();
        string connectionString = $"Data Source={databasePath}";
        string productId = CreateIdentifier("product");
        string customerId = CreateIdentifier("customer");
        string idempotencyKey = CreateIdentifier("payment");
        string serviceId = CreateIdentifier("ordering-persistence");
        string clusterId = CreateIdentifier("ordering-persistence");

        try {
            InProcessTestCluster firstCluster = await StartClusterAsync(
                connectionString,
                serviceId,
                clusterId);

            try {
                IInventoryItemGrain inventory = firstCluster.Client
                    .GetGrain<IInventoryItemGrain>(productId);
                IOrderGrain order = firstCluster.Client
                    .GetGrain<IOrderGrain>(Guid.NewGuid());

                await inventory.ResetAsync(10);
                GrainOrderResult result = await order.PlaceAsync(
                    idempotencyKey,
                    customerId,
                    productId,
                    quantity: 2,
                    simulatePaymentFailure: false);

                result.Status.ShouldBe(OrderStatus.Completed.ToString());
            } finally {
                await firstCluster.DisposeAsync();
            }

            InProcessTestCluster secondCluster = await StartClusterAsync(
                connectionString,
                serviceId,
                clusterId);

            try {
                IInventoryItemGrain inventory = secondCluster.Client
                    .GetGrain<IInventoryItemGrain>(productId);
                IOrderGrain order = secondCluster.Client
                    .GetGrain<IOrderGrain>(Guid.NewGuid());

                await inventory.ResetAsync(10);
                GrainOrderResult result = await order.PlaceAsync(
                    idempotencyKey,
                    customerId,
                    productId,
                    quantity: 2,
                    simulatePaymentFailure: true);
                InventorySnapshot inventoryAfterOrder = await inventory.GetAsync();

                result.Status.ShouldBe(OrderStatus.Completed.ToString());
                result.Reason.ShouldBeNull();
                inventoryAfterOrder.AvailableQuantity.ShouldBe(8);
            } finally {
                await secondCluster.DisposeAsync();
            }
        } finally {
            DeleteDatabaseFiles(databasePath);
        }
    }

    /// <summary>
    /// Verifies that clearing existing state removes its persisted record.
    /// </summary>
    [Fact]
    public async Task ClearingExistingStateRemovesRecord() {
        await using PersistenceTestContext context = await CreateContextAsync();
        IGrainStorage storage = GetStorage(context);
        GrainId grainId = GrainId.Create("test-inventory", context.CreateProductId());
        var grainState = new GrainState<InventoryItemState>(
            new InventoryItemState {
                AvailableQuantity = 10,
            });

        await storage.WriteStateAsync(InventoryStateName, grainId, grainState);
        await storage.ClearStateAsync(InventoryStateName, grainId, grainState);

        grainState.RecordExists.ShouldBeFalse();
        grainState.ETag.ShouldBeNull();

        var restoredState = new GrainState<InventoryItemState>(
            new InventoryItemState());
        await storage.ReadStateAsync(InventoryStateName, grainId, restoredState);

        restoredState.RecordExists.ShouldBeFalse();
        restoredState.ETag.ShouldBeNull();
        restoredState.State.AvailableQuantity.ShouldBe(0);
    }

    /// <summary>
    /// Verifies that clearing missing state remains an idempotent operation.
    /// </summary>
    [Fact]
    public async Task ClearingMissingStateLeavesStateMissing() {
        await using PersistenceTestContext context = await CreateContextAsync();
        IGrainStorage storage = GetStorage(context);
        GrainId grainId = GrainId.Create("test-inventory", context.CreateProductId());
        var grainState = new GrainState<InventoryItemState>(
            new InventoryItemState());

        await storage.ClearStateAsync(InventoryStateName, grainId, grainState);

        grainState.RecordExists.ShouldBeFalse();
        grainState.ETag.ShouldBeNull();
        grainState.State.AvailableQuantity.ShouldBe(0);
    }

    /// <summary>
    /// Verifies that identical grain keys remain isolated across grain types.
    /// </summary>
    [Fact]
    public async Task SameGrainKeyAcrossDifferentTypesPersistsIsolatedState() {
        await using PersistenceTestContext context = await CreateContextAsync();
        IGrainStorage storage = GetStorage(context);
        string grainKey = context.CreateProductId();
        GrainId firstGrainId = GrainId.Create("first-inventory", grainKey);
        GrainId secondGrainId = GrainId.Create("second-inventory", grainKey);
        var firstState = new GrainState<InventoryItemState>(
            new InventoryItemState {
                AvailableQuantity = 10,
            });
        var secondState = new GrainState<InventoryItemState>(
            new InventoryItemState {
                AvailableQuantity = 20,
            });

        await storage.WriteStateAsync(
            InventoryStateName,
            firstGrainId,
            firstState);
        await storage.WriteStateAsync(
            InventoryStateName,
            secondGrainId,
            secondState);

        var restoredFirstState = new GrainState<InventoryItemState>(
            new InventoryItemState());
        var restoredSecondState = new GrainState<InventoryItemState>(
            new InventoryItemState());

        await storage.ReadStateAsync(
            InventoryStateName,
            firstGrainId,
            restoredFirstState);
        await storage.ReadStateAsync(
            InventoryStateName,
            secondGrainId,
            restoredSecondState);

        restoredFirstState.State.AvailableQuantity.ShouldBe(10);
        restoredSecondState.State.AvailableQuantity.ShouldBe(20);
    }

    /// <summary>
    /// Verifies that different state names on one grain remain isolated.
    /// </summary>
    [Fact]
    public async Task SameGrainWithDifferentStateNamesPersistsIsolatedState() {
        await using PersistenceTestContext context = await CreateContextAsync();
        IGrainStorage storage = GetStorage(context);
        GrainId grainId = GrainId.Create(
            "test-inventory",
            context.CreateProductId());
        var firstState = new GrainState<InventoryItemState>(
            new InventoryItemState {
                AvailableQuantity = 10,
            });
        var secondState = new GrainState<InventoryItemState>(
            new InventoryItemState {
                AvailableQuantity = 20,
            });

        await storage.WriteStateAsync("first-state", grainId, firstState);
        await storage.WriteStateAsync("second-state", grainId, secondState);

        var restoredFirstState = new GrainState<InventoryItemState>(
            new InventoryItemState());
        var restoredSecondState = new GrainState<InventoryItemState>(
            new InventoryItemState());

        await storage.ReadStateAsync(
            "first-state",
            grainId,
            restoredFirstState);
        await storage.ReadStateAsync(
            "second-state",
            grainId,
            restoredSecondState);

        restoredFirstState.State.AvailableQuantity.ShouldBe(10);
        restoredSecondState.State.AvailableQuantity.ShouldBe(20);
    }

    /// <summary>
    /// Verifies that identical grain state remains isolated across Orleans service identifiers.
    /// </summary>
    [Fact]
    public async Task SameGrainStateAcrossDifferentServiceIdsRemainsIsolated() {
        string databasePath = CreateDatabasePath();
        string connectionString = $"Data Source={databasePath}";
        string firstServiceId = CreateIdentifier("ordering-persistence");
        string secondServiceId = CreateIdentifier("ordering-persistence");
        string clusterId = CreateIdentifier("ordering-persistence");
        GrainId grainId = GrainId.Create(
            "test-inventory",
            CreateIdentifier("product"));

        try {
            InProcessTestCluster firstCluster = await StartClusterAsync(
                connectionString,
                firstServiceId,
                clusterId);

            try {
                IGrainStorage firstStorage = GetStorage(firstCluster);
                var firstState = new GrainState<InventoryItemState>(
                    new InventoryItemState {
                        AvailableQuantity = 10,
                    });

                await firstStorage.WriteStateAsync(
                    InventoryStateName,
                    grainId,
                    firstState);
            } finally {
                await firstCluster.DisposeAsync();
            }

            InProcessTestCluster secondCluster = await StartClusterAsync(
                connectionString,
                secondServiceId,
                clusterId);

            try {
                IGrainStorage secondStorage = GetStorage(secondCluster);
                var secondState = new GrainState<InventoryItemState>(
                    new InventoryItemState());

                await secondStorage.ReadStateAsync(
                    InventoryStateName,
                    grainId,
                    secondState);

                secondState.RecordExists.ShouldBeFalse();
                secondState.State.AvailableQuantity.ShouldBe(0);
            } finally {
                await secondCluster.DisposeAsync();
            }
        } finally {
            DeleteDatabaseFiles(databasePath);
        }
    }

    /// <summary>
    /// Verifies that identical grain state remains isolated across storage providers.
    /// </summary>
    [Fact]
    public async Task SameGrainStateAcrossDifferentProvidersRemainsIsolated() {
        await using PersistenceTestContext context = await CreateContextAsync(
            registerSecondaryProvider: true);
        IGrainStorage primaryStorage = GetStorage(context);
        IGrainStorage secondaryStorage = GetStorage(
            context,
            SecondaryStorageProviderName);
        GrainId grainId = GrainId.Create(
            "test-inventory",
            context.CreateProductId());
        var primaryState = new GrainState<InventoryItemState>(
            new InventoryItemState {
                AvailableQuantity = 10,
            });
        var secondaryState = new GrainState<InventoryItemState>(
            new InventoryItemState {
                AvailableQuantity = 20,
            });

        await primaryStorage.WriteStateAsync(
            InventoryStateName,
            grainId,
            primaryState);
        await secondaryStorage.WriteStateAsync(
            InventoryStateName,
            grainId,
            secondaryState);

        var restoredPrimaryState = new GrainState<InventoryItemState>(
            new InventoryItemState());
        var restoredSecondaryState = new GrainState<InventoryItemState>(
            new InventoryItemState());

        await primaryStorage.ReadStateAsync(
            InventoryStateName,
            grainId,
            restoredPrimaryState);
        await secondaryStorage.ReadStateAsync(
            InventoryStateName,
            grainId,
            restoredSecondaryState);

        restoredPrimaryState.State.AvailableQuantity.ShouldBe(10);
        restoredSecondaryState.State.AvailableQuantity.ShouldBe(20);
    }

    /// <summary>
    /// Verifies that concurrent insertion permits only one writer for the same state.
    /// </summary>
    [Fact]
    public async Task ConcurrentInsertionAllowsOnlyOneWriter() {
        await using PersistenceTestContext context = await CreateContextAsync();
        IGrainStorage storage = GetStorage(context);
        GrainId grainId = GrainId.Create(
            "test-inventory",
            context.CreateProductId());
        var firstState = new GrainState<InventoryItemState>(
            new InventoryItemState {
                AvailableQuantity = 10,
            });
        var secondState = new GrainState<InventoryItemState>(
            new InventoryItemState {
                AvailableQuantity = 20,
            });

        Exception?[] exceptions = await Task.WhenAll(
            CaptureExceptionAsync(() => storage.WriteStateAsync(
                InventoryStateName,
                grainId,
                firstState)),
            CaptureExceptionAsync(() => storage.WriteStateAsync(
                InventoryStateName,
                grainId,
                secondState)));

        exceptions.Count(exception => exception is null).ShouldBe(1);
        exceptions.Count(exception => exception is InconsistentStateException)
            .ShouldBe(1);

        var restoredState = new GrainState<InventoryItemState>(
            new InventoryItemState());
        await storage.ReadStateAsync(
            InventoryStateName,
            grainId,
            restoredState);

        restoredState.RecordExists.ShouldBeTrue();
        restoredState.ETag.ShouldBe("1");
        restoredState.State.AvailableQuantity.ShouldBeOneOf(10, 20);
    }

    /// <summary>
    /// Verifies that concurrent updates permit only one writer for the same ETag.
    /// </summary>
    [Fact]
    public async Task ConcurrentUpdateAllowsOnlyOneWriter() {
        await using PersistenceTestContext context = await CreateContextAsync();
        IGrainStorage storage = GetStorage(context);
        GrainId grainId = GrainId.Create(
            "test-inventory",
            context.CreateProductId());
        var initialState = new GrainState<InventoryItemState>(
            new InventoryItemState {
                AvailableQuantity = 10,
            });

        await storage.WriteStateAsync(
            InventoryStateName,
            grainId,
            initialState);

        var firstState = new GrainState<InventoryItemState>(
            new InventoryItemState());
        var secondState = new GrainState<InventoryItemState>(
            new InventoryItemState());
        await storage.ReadStateAsync(
            InventoryStateName,
            grainId,
            firstState);
        await storage.ReadStateAsync(
            InventoryStateName,
            grainId,
            secondState);

        firstState.State.AvailableQuantity = 20;
        secondState.State.AvailableQuantity = 30;

        Exception?[] exceptions = await Task.WhenAll(
            CaptureExceptionAsync(() => storage.WriteStateAsync(
                InventoryStateName,
                grainId,
                firstState)),
            CaptureExceptionAsync(() => storage.WriteStateAsync(
                InventoryStateName,
                grainId,
                secondState)));

        exceptions.Count(exception => exception is null).ShouldBe(1);
        exceptions.Count(exception => exception is InconsistentStateException)
            .ShouldBe(1);

        var restoredState = new GrainState<InventoryItemState>(
            new InventoryItemState());
        await storage.ReadStateAsync(
            InventoryStateName,
            grainId,
            restoredState);

        restoredState.RecordExists.ShouldBeTrue();
        restoredState.ETag.ShouldBe("2");
        restoredState.State.AvailableQuantity.ShouldBeOneOf(20, 30);
    }

    /// <summary>
    /// Verifies that clearing state with a stale ETag is rejected.
    /// </summary>
    [Fact]
    public async Task StaleETagRejectsClear() {
        await using PersistenceTestContext context = await CreateContextAsync();
        IGrainStorage storage = GetStorage(context);
        GrainId grainId = GrainId.Create(
            "test-inventory",
            context.CreateProductId());
        var initialState = new GrainState<InventoryItemState>(
            new InventoryItemState {
                AvailableQuantity = 10,
            });

        await storage.WriteStateAsync(
            InventoryStateName,
            grainId,
            initialState);

        var currentState = new GrainState<InventoryItemState>(
            new InventoryItemState());
        var staleState = new GrainState<InventoryItemState>(
            new InventoryItemState());
        await storage.ReadStateAsync(
            InventoryStateName,
            grainId,
            currentState);
        await storage.ReadStateAsync(
            InventoryStateName,
            grainId,
            staleState);

        currentState.State.AvailableQuantity = 20;
        await storage.WriteStateAsync(
            InventoryStateName,
            grainId,
            currentState);

        await Should.ThrowAsync<InconsistentStateException>(
            async () => await storage.ClearStateAsync(
                InventoryStateName,
                grainId,
                staleState));

        var restoredState = new GrainState<InventoryItemState>(
            new InventoryItemState());
        await storage.ReadStateAsync(
            InventoryStateName,
            grainId,
            restoredState);

        restoredState.RecordExists.ShouldBeTrue();
        restoredState.ETag.ShouldBe("2");
        restoredState.State.AvailableQuantity.ShouldBe(20);
    }

    /// <summary>
    /// Verifies that multiple providers can initialize one new SQLite schema concurrently.
    /// </summary>
    [Fact]
    public async Task ConcurrentSchemaInitializationSucceeds() {
        await using PersistenceTestContext context = await CreateContextAsync(
            registerSecondaryProvider: true);

        GetStorage(context).ShouldNotBeNull();
        GetStorage(context, SecondaryStorageProviderName).ShouldNotBeNull();

        long tableCount = await CountTableAsync(
            context.ConnectionString,
            "GrainStates");

        tableCount.ShouldBe(1);
    }

    /// <summary>
    /// Verifies that a failed stale write does not replace the persisted payload.
    /// </summary>
    [Fact]
    public async Task FailedWritePreservesPersistedPayloadAfterRestart() {
        string databasePath = CreateDatabasePath();
        string connectionString = $"Data Source={databasePath}";
        string serviceId = CreateIdentifier("ordering-persistence");
        string clusterId = CreateIdentifier("ordering-persistence");
        GrainId grainId = GrainId.Create(
            "test-inventory",
            CreateIdentifier("product"));

        try {
            InProcessTestCluster firstCluster = await StartClusterAsync(
                connectionString,
                serviceId,
                clusterId);

            try {
                IGrainStorage storage = GetStorage(firstCluster);
                var initialState = new GrainState<InventoryItemState>(
                    new InventoryItemState {
                        AvailableQuantity = 10,
                    });
                await storage.WriteStateAsync(
                    InventoryStateName,
                    grainId,
                    initialState);

                var currentState = new GrainState<InventoryItemState>(
                    new InventoryItemState());
                var staleState = new GrainState<InventoryItemState>(
                    new InventoryItemState());
                await storage.ReadStateAsync(
                    InventoryStateName,
                    grainId,
                    currentState);
                await storage.ReadStateAsync(
                    InventoryStateName,
                    grainId,
                    staleState);

                currentState.State.AvailableQuantity = 20;
                await storage.WriteStateAsync(
                    InventoryStateName,
                    grainId,
                    currentState);

                staleState.State.AvailableQuantity = 30;
                await Should.ThrowAsync<InconsistentStateException>(
                    async () => await storage.WriteStateAsync(
                        InventoryStateName,
                        grainId,
                        staleState));
            } finally {
                await firstCluster.DisposeAsync();
            }

            InProcessTestCluster secondCluster = await StartClusterAsync(
                connectionString,
                serviceId,
                clusterId);

            try {
                IGrainStorage storage = GetStorage(secondCluster);
                var restoredState = new GrainState<InventoryItemState>(
                    new InventoryItemState());

                await storage.ReadStateAsync(
                    InventoryStateName,
                    grainId,
                    restoredState);

                restoredState.RecordExists.ShouldBeTrue();
                restoredState.ETag.ShouldBe("2");
                restoredState.State.AvailableQuantity.ShouldBe(20);
            } finally {
                await secondCluster.DisposeAsync();
            }
        } finally {
            DeleteDatabaseFiles(databasePath);
        }
    }

    /// <summary>
    /// Verifies that writing state after clearing creates a new version-one record.
    /// </summary>
    [Fact]
    public async Task ClearFollowedByNewWriteCreatesVersionOne() {
        await using PersistenceTestContext context = await CreateContextAsync();
        IGrainStorage storage = GetStorage(context);
        GrainId grainId = GrainId.Create(
            "test-inventory",
            context.CreateProductId());
        var state = new GrainState<InventoryItemState>(
            new InventoryItemState {
                AvailableQuantity = 10,
            });

        await storage.WriteStateAsync(InventoryStateName, grainId, state);
        await storage.ClearStateAsync(InventoryStateName, grainId, state);

        state.State = new InventoryItemState {
            AvailableQuantity = 20,
        };
        await storage.WriteStateAsync(InventoryStateName, grainId, state);

        state.RecordExists.ShouldBeTrue();
        state.ETag.ShouldBe("1");

        var restoredState = new GrainState<InventoryItemState>(
            new InventoryItemState());
        await storage.ReadStateAsync(
            InventoryStateName,
            grainId,
            restoredState);

        restoredState.ETag.ShouldBe("1");
        restoredState.State.AvailableQuantity.ShouldBe(20);
    }

    /// <summary>
    /// Verifies that nontrivial inventory state survives a cluster restart.
    /// </summary>
    [Fact]
    public async Task ComplexInventoryStateSurvivesClusterRestart() {
        string databasePath = CreateDatabasePath();
        string connectionString = $"Data Source={databasePath}";
        string serviceId = CreateIdentifier("ordering-persistence");
        string clusterId = CreateIdentifier("ordering-persistence");
        GrainId grainId = GrainId.Create(
            "test-inventory",
            CreateIdentifier("product"));
        Guid firstReservationId = Guid.NewGuid();
        Guid secondReservationId = Guid.NewGuid();

        try {
            InProcessTestCluster firstCluster = await StartClusterAsync(
                connectionString,
                serviceId,
                clusterId);

            try {
                IGrainStorage storage = GetStorage(firstCluster);
                var state = new GrainState<InventoryItemState>(
                    new InventoryItemState {
                        AvailableQuantity = 12,
                        Reservations = {
                            [firstReservationId] = 3,
                            [secondReservationId] = 5,
                        },
                    });

                await storage.WriteStateAsync(
                    InventoryStateName,
                    grainId,
                    state);
            } finally {
                await firstCluster.DisposeAsync();
            }

            InProcessTestCluster secondCluster = await StartClusterAsync(
                connectionString,
                serviceId,
                clusterId);

            try {
                IGrainStorage storage = GetStorage(secondCluster);
                var restoredState = new GrainState<InventoryItemState>(
                    new InventoryItemState());

                await storage.ReadStateAsync(
                    InventoryStateName,
                    grainId,
                    restoredState);

                restoredState.State.AvailableQuantity.ShouldBe(12);
                restoredState.State.Reservations.Count.ShouldBe(2);
                restoredState.State.Reservations[firstReservationId].ShouldBe(3);
                restoredState.State.Reservations[secondReservationId].ShouldBe(5);
            } finally {
                await secondCluster.DisposeAsync();
            }
        } finally {
            DeleteDatabaseFiles(databasePath);
        }
    }

    /// <summary>
    /// Verifies that an existing empty SQLite database receives the grain-state schema.
    /// </summary>
    [Fact]
    public async Task ExistingEmptyDatabaseInitializesSchema() {
        string databasePath = CreateDatabasePath();
        string connectionString = $"Data Source={databasePath}";
        string serviceId = CreateIdentifier("ordering-persistence");
        string clusterId = CreateIdentifier("ordering-persistence");

        try {
            await using (var connection = new SqliteConnection(connectionString)) {
                await connection.OpenAsync();
            }

            File.Exists(databasePath).ShouldBeTrue();
            (await CountTableAsync(connectionString, "GrainStates")).ShouldBe(0);

            InProcessTestCluster cluster = await StartClusterAsync(
                connectionString,
                serviceId,
                clusterId);

            try {
                (await CountTableAsync(connectionString, "GrainStates"))
                    .ShouldBe(1);
            } finally {
                await cluster.DisposeAsync();
            }
        } finally {
            DeleteDatabaseFiles(databasePath);
        }
    }

    /// <summary>
    /// Verifies that persisted inventory state is restored after restarting the cluster.
    /// </summary>
    [Fact]
    public async Task InventoryStateSurvivesClusterRestart() {
        string databasePath = CreateDatabasePath();
        string connectionString = $"Data Source={databasePath}";
        string productId = CreateIdentifier("product");
        string serviceId = CreateIdentifier("ordering-persistence");
        string clusterId = CreateIdentifier("ordering-persistence");

        try {
            InProcessTestCluster firstCluster = await StartClusterAsync(
                connectionString,
                serviceId,
                clusterId);

            try {
                IInventoryItemGrain inventory = firstCluster.Client
                    .GetGrain<IInventoryItemGrain>(productId);

                await inventory.ResetAsync(10);
                await inventory.ReserveAsync(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    quantity: 3);
            } finally {
                await firstCluster.DisposeAsync();
            }

            InProcessTestCluster secondCluster = await StartClusterAsync(
                connectionString,
                serviceId,
                clusterId);

            try {
                IInventoryItemGrain inventory = secondCluster.Client
                    .GetGrain<IInventoryItemGrain>(productId);

                (await inventory.GetAsync()).AvailableQuantity.ShouldBe(7);
            } finally {
                await secondCluster.DisposeAsync();
            }
        } finally {
            DeleteDatabaseFiles(databasePath);
        }
    }

    private static async Task<long> CountTableAsync(
        string connectionString,
        string tableName) {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name = $tableName;
            """;
        command.Parameters.AddWithValue("$tableName", tableName);

        object? result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async Task<Exception?> CaptureExceptionAsync(
        Func<Task> action) {
        try {
            await action();
            return null;
        } catch (Exception exception) {
            return exception;
        }
    }

    private static IGrainStorage GetStorage(
        PersistenceTestContext context,
        string storageProviderName = StorageProviderName) {
        return GetStorage(context.Cluster, storageProviderName);
    }

    private static IGrainStorage GetStorage(
        InProcessTestCluster cluster,
        string storageProviderName = StorageProviderName) {
        IServiceProvider serviceProvider =
            cluster.GetSiloServiceProvider();

        return serviceProvider.GetRequiredKeyedService<IGrainStorage>(
            storageProviderName);
    }

    private static async Task<PersistenceTestContext> CreateContextAsync(
        bool registerSecondaryProvider = false) {
        string databasePath = CreateDatabasePath();
        string connectionString = $"Data Source={databasePath}";
        string serviceId = CreateIdentifier("ordering-persistence");
        string clusterId = CreateIdentifier("ordering-persistence");

        try {
            InProcessTestCluster cluster = await StartClusterAsync(
                connectionString,
                serviceId,
                clusterId,
                registerSecondaryProvider);

            return new PersistenceTestContext(
                cluster,
                databasePath,
                connectionString,
                serviceId);
        } catch {
            DeleteDatabaseFiles(databasePath);
            throw;
        }
    }

    private static string CreateDatabasePath() {
        return Path.Combine(
            Path.GetTempPath(),
            $"ordering-grain-state-{Guid.NewGuid():N}.db");
    }

    private static string CreateIdentifier(string prefix) {
        return $"{prefix}-{Guid.NewGuid():N}";
    }

    private static async Task<InProcessTestCluster> StartClusterAsync(
        string connectionString,
        string serviceId,
        string clusterId,
        bool registerSecondaryProvider = false) {
        var builder = new InProcessTestClusterBuilder();

        builder.Options.ServiceId = serviceId;
        builder.Options.ClusterId = clusterId;

        builder.ConfigureSilo((_, siloBuilder) => {
            siloBuilder.AddSqliteGrainStorage(
                StorageProviderName,
                connectionString);

            if (registerSecondaryProvider) {
                siloBuilder.AddSqliteGrainStorage(
                    SecondaryStorageProviderName,
                    connectionString);
            }
        });

        InProcessTestCluster cluster = builder.Build();
        await cluster.DeployAsync();
        return cluster;
    }

    private static Task<long> CountInventoryRecordsAsync(
        PersistenceTestContext context,
        string productId) {
        return ExecuteScalarAsync(
            context,
            productId,
            "COUNT(*)");
    }

    private static Task<long> ReadInventoryVersionAsync(
        PersistenceTestContext context,
        string productId) {
        return ExecuteScalarAsync(
            context,
            productId,
            "Version");
    }

    private static async Task<long> ExecuteScalarAsync(
        PersistenceTestContext context,
        string productId,
        string selection) {
        await using var connection = new SqliteConnection(context.ConnectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {selection}
            FROM GrainStates
            WHERE ServiceId = $serviceId
              AND ProviderName = $providerName
              AND StateName = $stateName
              AND GrainId = $grainId;
            """;
        AddInventoryParameters(command, context, productId);

        object? result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async Task SetInventoryVersionAsync(
        PersistenceTestContext context,
        string productId,
        int version) {
        await using var connection = new SqliteConnection(context.ConnectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE GrainStates
            SET Version = $version
            WHERE ServiceId = $serviceId
              AND ProviderName = $providerName
              AND StateName = $stateName
              AND GrainId = $grainId;
            """;
        command.Parameters.AddWithValue("$version", version);
        AddInventoryParameters(command, context, productId);

        int affectedRows = await command.ExecuteNonQueryAsync();
        affectedRows.ShouldBe(1);
    }

    private static void AddInventoryParameters(
        SqliteCommand command,
        PersistenceTestContext context,
        string productId) {
        command.Parameters.AddWithValue("$serviceId", context.ServiceId);
        command.Parameters.AddWithValue("$providerName", StorageProviderName);
        command.Parameters.AddWithValue("$stateName", InventoryStateName);
        command.Parameters.AddWithValue("$grainId", productId);
    }

    private static void DeleteDatabaseFiles(string databasePath) {
        SqliteConnection.ClearAllPools();

        foreach (string path in new[] {
            databasePath,
            $"{databasePath}-shm",
            $"{databasePath}-wal",
        }) {
            DeleteFileIfExists(path);
        }
    }

    private static void DeleteFileIfExists(string path) {
        const int MaxAttempts = 5;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++) {
            try {
                if (File.Exists(path)) {
                    File.Delete(path);
                }

                return;
            } catch (IOException) when (attempt < MaxAttempts) {
                Thread.Sleep(TimeSpan.FromMilliseconds(100 * attempt));
            }
        }
    }

    private sealed class PersistenceTestContext(
        InProcessTestCluster cluster,
        string databasePath,
        string connectionString,
        string serviceId)
        : IAsyncDisposable {
        public InProcessTestCluster Cluster { get; } = cluster;

        public string ConnectionString { get; } = connectionString;

        public string ServiceId { get; } = serviceId;

        public string CreateProductId() {
            return CreateIdentifier("product");
        }

        public async ValueTask DisposeAsync() {
            await Cluster.DisposeAsync();
            DeleteDatabaseFiles(databasePath);
        }
    }
}
