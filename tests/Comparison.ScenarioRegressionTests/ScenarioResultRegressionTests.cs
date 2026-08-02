namespace Comparison.ScenarioRegressionTests;

using Comparison.Contracts;
using Comparison.Gateway.Clients;
using Comparison.Gateway.Scenarios;
using Shouldly;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

/// <summary>
/// Regression tests for scenario result metrics produced by the comparison gateway client.
/// </summary>
public sealed class ScenarioResultRegressionTests {
    /// <summary>
    /// Verifies that a successful order reports one request submission and one unique successful order.
    /// </summary>
    [Fact]
    public async Task SuccessfulOrderReportsOneUniqueSuccessfulOrder() {
        (ScenarioRunner runner, IServiceClient client) = CreateRunner();
        RunScenarioRequest request = CreateRequest(ScenarioKind.SuccessfulOrder, initialStock: 10, quantity: 2, concurrentRequests: 10);

        ScenarioExecutionResult result = await runner.RunAsync(client, request, CancellationToken.None);

        result.TotalRequestSubmissions.ShouldBe(1);
        result.CompletedOrders.ShouldBe(1);
        result.RejectedOrders.ShouldBe(0);
        result.IdempotentResponses.ShouldBe(0);
        result.RemainingInventory.ShouldBe(8);
        result.Status.ShouldBe(OrderStatus.Completed);
    }

    /// <summary>
    /// Verifies that insufficient inventory reports one rejected submission without reducing inventory.
    /// </summary>
    [Fact]
    public async Task InsufficientInventoryRejectsOneSubmissionAndLeavesInventoryUnchanged() {
        (ScenarioRunner runner, IServiceClient client) = CreateRunner();
        RunScenarioRequest request = CreateRequest(ScenarioKind.InsufficientInventory, initialStock: 1, quantity: 2, concurrentRequests: 10);

        ScenarioExecutionResult result = await runner.RunAsync(client, request, CancellationToken.None);

        result.TotalRequestSubmissions.ShouldBe(1);
        result.CompletedOrders.ShouldBe(0);
        result.RejectedOrders.ShouldBe(1);
        result.IdempotentResponses.ShouldBe(0);
        result.RemainingInventory.ShouldBe(1);
        result.Status.ShouldBe(OrderStatus.Rejected);
        result.Reason.ShouldBe("InsufficientInventory");
    }

    /// <summary>
    /// Verifies that payment failure compensation releases the reserved inventory.
    /// </summary>
    [Fact]
    public async Task PaymentFailureCompensationReleasesInventoryAndRejectsSubmission() {
        (ScenarioRunner runner, IServiceClient client) = CreateRunner();
        RunScenarioRequest request = CreateRequest(ScenarioKind.PaymentFailureCompensation, initialStock: 10, quantity: 2, concurrentRequests: 10);

        ScenarioExecutionResult result = await runner.RunAsync(client, request, CancellationToken.None);

        result.TotalRequestSubmissions.ShouldBe(1);
        result.CompletedOrders.ShouldBe(0);
        result.RejectedOrders.ShouldBe(1);
        result.IdempotentResponses.ShouldBe(0);
        result.RemainingInventory.ShouldBe(10);
        result.Status.ShouldBe(OrderStatus.Rejected);
        result.Reason.ShouldBe("PaymentFailed");
    }

    /// <summary>
    /// Verifies that payment timeout after reservation is reported as a timeout and releases inventory.
    /// </summary>
    [Fact]
    public async Task PaymentTimeoutAfterReservationReportsTimeoutAndReleasesInventory() {
        (ScenarioRunner runner, IServiceClient client) = CreateRunner();
        RunScenarioRequest request = CreateRequest(ScenarioKind.PaymentTimeoutAfterReservation, initialStock: 10, quantity: 2, concurrentRequests: 10);

        ScenarioExecutionResult result = await runner.RunAsync(client, request, CancellationToken.None);

        result.TotalRequestSubmissions.ShouldBe(1);
        result.CompletedOrders.ShouldBe(0);
        result.RejectedOrders.ShouldBe(1);
        result.IdempotentResponses.ShouldBe(0);
        result.RemainingInventory.ShouldBe(10);
        result.Status.ShouldBe(OrderStatus.Rejected);
        result.Reason.ShouldBe("PaymentTimeout");
        result.Events.ShouldContain(scenarioEvent => scenarioEvent.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies that hot product contention does not over-reserve inventory.
    /// </summary>
    [Fact]
    public async Task HotProductContentionDoesNotOverReserveInventory() {
        (ScenarioRunner runner, IServiceClient client) = CreateRunner();
        RunScenarioRequest request = CreateRequest(ScenarioKind.HotProductContention, initialStock: 25, quantity: 1, concurrentRequests: 50);

        ScenarioExecutionResult result = await runner.RunAsync(client, request, CancellationToken.None);

        result.TotalRequestSubmissions.ShouldBe(50);
        result.CompletedOrders.ShouldBe(25);
        result.RejectedOrders.ShouldBe(25);
        result.IdempotentResponses.ShouldBe(0);
        result.RemainingInventory.ShouldBe(0);
        result.Reason.ShouldBe("SomeOrdersRejected");
    }

    /// <summary>
    /// Verifies that duplicate request uses concurrent requests as the duplicate submission count.
    /// </summary>
    [Fact]
    public async Task DuplicateRequestUsesConcurrentRequestsAsDuplicateSubmissionCount() {
        (ScenarioRunner runner, IServiceClient client) = CreateRunner();
        RunScenarioRequest request = CreateRequest(ScenarioKind.DuplicateRequest, initialStock: 10, quantity: 2, concurrentRequests: 20);

        ScenarioExecutionResult result = await runner.RunAsync(client, request, CancellationToken.None);

        result.TotalRequestSubmissions.ShouldBe(20);
        result.CompletedOrders.ShouldBe(1);
        result.RejectedOrders.ShouldBe(0);
        result.IdempotentResponses.ShouldBe(19);
        result.RemainingInventory.ShouldBe(8);
        result.Status.ShouldBe(OrderStatus.Completed);
        result.Reason.ShouldBe("IdempotentResultReturned");
    }

    /// <summary>
    /// Verifies that repeating a successful scenario reuses the persisted result for the same customer and idempotency key.
    /// </summary>
    [Fact]
    public async Task RepeatedSuccessfulOrderReusesPersistedResult() {
        (ScenarioRunner runner, IServiceClient client) = CreateRunner();
        RunScenarioRequest firstRequest = CreateRequest(
            ScenarioKind.SuccessfulOrder,
            initialStock: 10,
            quantity: 2,
            concurrentRequests: 10);
        RunScenarioRequest secondRequest = firstRequest with {
            OrderId = Guid.NewGuid(),
        };

        ScenarioExecutionResult firstResult = await runner.RunAsync(
            client,
            firstRequest,
            CancellationToken.None);
        ScenarioExecutionResult secondResult = await runner.RunAsync(
            client,
            secondRequest,
            CancellationToken.None);

        firstResult.Status.ShouldBe(OrderStatus.Completed);
        firstResult.RemainingInventory.ShouldBe(8);
        secondResult.Status.ShouldBe(OrderStatus.Completed);
        secondResult.RemainingInventory.ShouldBe(10);
    }

    /// <summary>
    /// Verifies that repeating a failed-payment scenario reuses the persisted rejection.
    /// </summary>
    [Fact]
    public async Task RepeatedPaymentFailureReusesPersistedRejection() {
        (ScenarioRunner runner, IServiceClient client) = CreateRunner();
        RunScenarioRequest firstRequest = CreateRequest(
            ScenarioKind.PaymentFailureCompensation,
            initialStock: 10,
            quantity: 2,
            concurrentRequests: 10);
        RunScenarioRequest secondRequest = firstRequest with {
            OrderId = Guid.NewGuid(),
        };

        ScenarioExecutionResult firstResult = await runner.RunAsync(
            client,
            firstRequest,
            CancellationToken.None);
        ScenarioExecutionResult secondResult = await runner.RunAsync(
            client,
            secondRequest,
            CancellationToken.None);

        firstResult.Status.ShouldBe(OrderStatus.Rejected);
        firstResult.Reason.ShouldBe("PaymentFailed");
        firstResult.RemainingInventory.ShouldBe(10);
        secondResult.Status.ShouldBe(OrderStatus.Rejected);
        secondResult.Reason.ShouldBe("PaymentFailed");
        secondResult.RemainingInventory.ShouldBe(10);
    }

    /// <summary>
    /// Verifies that a persisted successful result remains authoritative when a later request simulates payment failure.
    /// </summary>
    [Fact]
    public async Task SuccessfulPaymentRemainsAuthoritativeForRepeatedKey() {
        (ScenarioRunner runner, IServiceClient client) = CreateRunner();
        RunScenarioRequest successfulRequest = CreateRequest(
            ScenarioKind.SuccessfulOrder,
            initialStock: 10,
            quantity: 2,
            concurrentRequests: 10);
        RunScenarioRequest failureRequest = successfulRequest with {
            Scenario = ScenarioKind.PaymentFailureCompensation,
            OrderId = Guid.NewGuid(),
            SimulatePaymentFailure = true,
        };

        ScenarioExecutionResult successfulResult = await runner.RunAsync(
            client,
            successfulRequest,
            CancellationToken.None);
        ScenarioExecutionResult repeatedResult = await runner.RunAsync(
            client,
            failureRequest,
            CancellationToken.None);

        successfulResult.Status.ShouldBe(OrderStatus.Completed);
        successfulResult.RemainingInventory.ShouldBe(8);
        repeatedResult.Status.ShouldBe(OrderStatus.Completed);
        repeatedResult.Reason.ShouldBeNull();
        repeatedResult.RemainingInventory.ShouldBe(10);
    }

    /// <summary>
    /// Verifies that a persisted failed result remains authoritative when a later request would otherwise succeed.
    /// </summary>
    [Fact]
    public async Task FailedPaymentRemainsAuthoritativeForRepeatedKey() {
        (ScenarioRunner runner, IServiceClient client) = CreateRunner();
        RunScenarioRequest failureRequest = CreateRequest(
            ScenarioKind.PaymentFailureCompensation,
            initialStock: 10,
            quantity: 2,
            concurrentRequests: 10);
        RunScenarioRequest successfulRequest = failureRequest with {
            Scenario = ScenarioKind.SuccessfulOrder,
            OrderId = Guid.NewGuid(),
            SimulatePaymentFailure = false,
        };

        ScenarioExecutionResult failureResult = await runner.RunAsync(
            client,
            failureRequest,
            CancellationToken.None);
        ScenarioExecutionResult repeatedResult = await runner.RunAsync(
            client,
            successfulRequest,
            CancellationToken.None);

        failureResult.Status.ShouldBe(OrderStatus.Rejected);
        failureResult.Reason.ShouldBe("PaymentFailed");
        failureResult.RemainingInventory.ShouldBe(10);
        repeatedResult.Status.ShouldBe(OrderStatus.Rejected);
        repeatedResult.Reason.ShouldBe("PaymentFailed");
        repeatedResult.RemainingInventory.ShouldBe(10);
    }

    /// <summary>
    /// Verifies that the same idempotency key remains isolated across customers.
    /// </summary>
    [Fact]
    public async Task RepeatedIdempotencyKeyRemainsIsolatedAcrossCustomers() {
        (ScenarioRunner runner, IServiceClient client) = CreateRunner();
        RunScenarioRequest firstRequest = CreateRequest(
            ScenarioKind.SuccessfulOrder,
            initialStock: 10,
            quantity: 2,
            concurrentRequests: 10);
        RunScenarioRequest secondRequest = firstRequest with {
            CustomerId = "customer-002",
            OrderId = Guid.NewGuid(),
        };

        ScenarioExecutionResult firstResult = await runner.RunAsync(
            client,
            firstRequest,
            CancellationToken.None);
        ScenarioExecutionResult secondResult = await runner.RunAsync(
            client,
            secondRequest,
            CancellationToken.None);

        firstResult.Status.ShouldBe(OrderStatus.Completed);
        firstResult.RemainingInventory.ShouldBe(8);
        secondResult.Status.ShouldBe(OrderStatus.Completed);
        secondResult.RemainingInventory.ShouldBe(8);
    }

    /// <summary>
    /// Verifies that a duplicate-request scenario can be repeated with persisted idempotency state.
    /// </summary>
    [Fact]
    public async Task DuplicateRequestCanBeRepeatedWithPersistedResult() {
        (ScenarioRunner runner, IServiceClient client) = CreateRunner();
        RunScenarioRequest firstRequest = CreateRequest(
            ScenarioKind.DuplicateRequest,
            initialStock: 10,
            quantity: 2,
            concurrentRequests: 20);
        RunScenarioRequest secondRequest = firstRequest with {
            OrderId = Guid.NewGuid(),
        };

        ScenarioExecutionResult firstResult = await runner.RunAsync(
            client,
            firstRequest,
            CancellationToken.None);
        ScenarioExecutionResult secondResult = await runner.RunAsync(
            client,
            secondRequest,
            CancellationToken.None);

        firstResult.CompletedOrders.ShouldBe(1);
        firstResult.IdempotentResponses.ShouldBe(19);
        firstResult.RemainingInventory.ShouldBe(8);
        secondResult.CompletedOrders.ShouldBe(1);
        secondResult.IdempotentResponses.ShouldBe(19);
        secondResult.RemainingInventory.ShouldBe(10);
    }

    private static (ScenarioRunner Runner, IServiceClient Client) CreateRunner() {
        var handler = new ScenarioRegressionHttpMessageHandler();
        var httpClient = new HttpClient(handler) {
            BaseAddress = new Uri("http://scenario-regression.test"),
        };

        return (new ScenarioRunner(), new RegressionServiceClient(httpClient));
    }

    private static RunScenarioRequest CreateRequest(
        ScenarioKind scenario,
        int initialStock,
        int quantity,
        int concurrentRequests) {
        return new RunScenarioRequest {
            Scenario = scenario,
            ProductId = "product-001",
            CustomerId = "customer-001",
            InitialStock = initialStock,
            Quantity = quantity,
            ConcurrentRequests = concurrentRequests,
            IdempotencyKey = $"{scenario}-{Guid.NewGuid():N}",
            SimulatePaymentFailure = scenario is ScenarioKind.PaymentFailureCompensation or ScenarioKind.PaymentTimeoutAfterReservation,
        };
    }

    private sealed class RegressionServiceClient(HttpClient httpClient)
        : HttpServiceClient(httpClient, "Microservices") {
        /// <inheritdoc />
        public override IReadOnlyList<ScenarioEvent> CreateTimeline(
            RunScenarioRequest request,
            OrderResponse order,
            InventoryResponse inventory) {
            return
            [
                new ScenarioEvent("Scenario", $"Processed {request.Scenario} with status {order.Status}."),
                new ScenarioEvent("Inventory", $"Remaining inventory is {inventory.AvailableQuantity}."),
            ];
        }
    }

    private sealed class ScenarioRegressionHttpMessageHandler : HttpMessageHandler {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly ConcurrentDictionary<string, int> inventoryByProductId = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, StoredOrder> ordersByIdempotencyKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim gate = new(1, 1);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (request.Method == HttpMethod.Post && path.Equals("/api/scenarios/reset", StringComparison.OrdinalIgnoreCase)) {
                ResetInventoryRequest reset = await ReadJsonAsync<ResetInventoryRequest>(request, cancellationToken).ConfigureAwait(false);
                inventoryByProductId[reset.ProductId] = reset.Quantity;
                return Json(new { ok = true });
            }

            if (request.Method == HttpMethod.Post && path.Equals("/api/orders", StringComparison.OrdinalIgnoreCase)) {
                RunScenarioRequest orderRequest = await ReadJsonAsync<RunScenarioRequest>(request, cancellationToken).ConfigureAwait(false);
                return await PlaceOrderAsync(orderRequest, cancellationToken).ConfigureAwait(false);
            }

            if (request.Method == HttpMethod.Get && path.StartsWith("/api/inventory/", StringComparison.OrdinalIgnoreCase)) {
                var productId = Uri.UnescapeDataString(path.Split('/').Last());
                var quantity = inventoryByProductId.GetValueOrDefault(productId);
                return Json(new InventoryResponse(productId, quantity));
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound) {
                Content = new StringContent($"No fake route for {request.Method} {path}"),
            };
        }

        private async Task<HttpResponseMessage> PlaceOrderAsync(RunScenarioRequest request, CancellationToken cancellationToken) {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                string idempotencyIdentity = CreateIdempotencyIdentity(request);
                if (ordersByIdempotencyKey.TryGetValue(
                    idempotencyIdentity,
                    out StoredOrder? existing)) {
                    return Json(ToOrderResponse(existing));
                }

                var currentInventory = inventoryByProductId.GetValueOrDefault(request.ProductId);
                if (currentInventory < request.Quantity) {
                    var rejected = new StoredOrder(request.OrderId, OrderStatus.Rejected, "InsufficientInventory");
                    ordersByIdempotencyKey.TryAdd(idempotencyIdentity, rejected);
                    return Json(ToOrderResponse(rejected));
                }

                inventoryByProductId[request.ProductId] = currentInventory - request.Quantity;

                if (request.SimulatePaymentFailure) {
                    inventoryByProductId[request.ProductId] += request.Quantity;
                    var paymentRejected = new StoredOrder(request.OrderId, OrderStatus.Rejected, "PaymentFailed");
                    ordersByIdempotencyKey.TryAdd(idempotencyIdentity, paymentRejected);
                    return Json(ToOrderResponse(paymentRejected));
                }

                var completed = new StoredOrder(request.OrderId, OrderStatus.Completed, Reason: null);
                ordersByIdempotencyKey.TryAdd(idempotencyIdentity, completed);
                return Json(ToOrderResponse(completed));
            } finally {
                gate.Release();
            }
        }

        private static string CreateIdempotencyIdentity(
            RunScenarioRequest request) {
            return $"{request.CustomerId}:{request.IdempotencyKey}";
        }

        private static OrderResponse ToOrderResponse(StoredOrder order) {
            return new OrderResponse(order.OrderId, order.Status, order.Reason);
        }

        private static async Task<T> ReadJsonAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken) {
            Stream stream = await request.Content!.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false)) {
                return (await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false))!;
            }
        }

        private static HttpResponseMessage Json<T>(T payload) {
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
            };
        }

        private sealed record StoredOrder(Guid OrderId, OrderStatus Status, string? Reason);
    }
}



