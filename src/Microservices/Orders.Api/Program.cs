using Comparison.Contracts;
using Hosting.ServiceDefaults.Extensions;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Clients;
using Orders.Api.Clients.Abstraction;
using Orders.Api.Data;
using Orders.Api.Logging;
using Orders.Api.Models;
using System.Collections.Concurrent;
using System.Text.Json;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add shared Aspire service discovery, resilience, health checks, and OpenTelemetry.
builder.AddServiceDefaults();

builder.Services.AddDbContext<OrdersDbContext>(options => {
    var connectionString = builder.Configuration.GetConnectionString($"Default") ?? $"Data Source=orders.db";
    options.UseSqlite(connectionString);
});

builder.Services.AddHttpClient<IInventoryClient, HttpInventoryClient>(client => {
    var baseUrl = builder.Configuration[$"Services:InventoryBaseUrl"] ?? $"http://localhost:5201";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddHttpClient<IPaymentsClient, HttpPaymentsClient>(client => {
    var baseUrl = builder.Configuration[$"Services:PaymentsBaseUrl"] ?? $"http://localhost:5202";
    client.BaseAddress = new Uri(baseUrl);
});

WebApplication app = builder.Build();
// correlation-id-logging
app.Use(async (context, next) => {
    var correlationId = context.Request.Headers[$"X-Correlation-ID"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId)) {
        await next().ConfigureAwait(false);
        return;
    }

    using IDisposable? scope = app.Logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal) {
        [$"CorrelationId"] = correlationId,
    });

    app.Logger.HandlingRequestWithCorrelationId(correlationId);

    await next().ConfigureAwait(false);
});

// orders-api-idempotency-keyed-gate
var orderIdempotencyLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);
app.Use(async (context, next) => {
    if (!HttpMethods.IsPost(context.Request.Method) || !context.Request.Path.Equals($"/api/orders", StringComparison.OrdinalIgnoreCase)) {
        await next().ConfigureAwait(false);
        return;
    }

    context.Request.EnableBuffering();
    RunScenarioRequest? request = null;
    try {
        request = await JsonSerializer.DeserializeAsync<RunScenarioRequest>(
            context.Request.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            context.RequestAborted).ConfigureAwait(false);
    } finally {
        context.Request.Body.Position = 0;
    }

    if (string.IsNullOrWhiteSpace(request?.IdempotencyKey)) {
        await next().ConfigureAwait(false);
        return;
    }

    SemaphoreSlim requestLock = orderIdempotencyLocks.GetOrAdd(request.IdempotencyKey, _ => new SemaphoreSlim(1, 1));
    await requestLock.WaitAsync(context.RequestAborted).ConfigureAwait(false);
    try {
        await next().ConfigureAwait(false);
    } finally {
        requestLock.Release();
        if (requestLock.CurrentCount == 1) {
            orderIdempotencyLocks.TryRemove(request.IdempotencyKey, out _);
        }
    }
});

await EnsureDatabaseAsync(app.Services).ConfigureAwait(false);

app.MapGet($"/", () => Results.Ok(new { Name = $"Orders API", Phase = $"Microservices" }));
app.MapGet($"/health/live", () => Results.Ok($"Healthy"));

app.MapPost($"/api/scenarios/reset", async (
    ResetInventoryRequest request,
    IInventoryClient inventoryClient,
    OrdersDbContext db,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) => {
        ILogger logger = loggerFactory.CreateLogger("Orders.ResetInventory");

        logger.ResettingInventory(request.ProductId, request.Quantity);

        try {
            db.Orders.RemoveRange(await db.Orders.ToListAsync(cancellationToken).ConfigureAwait(false));
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            InventoryResponse inventory = await inventoryClient.ResetAsync(request, cancellationToken).ConfigureAwait(false);

            logger.InventoryReset(inventory.ProductId, inventory.AvailableQuantity);

            return Results.Ok(inventory);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.InventoryResetFailed(exception, request.ProductId, request.Quantity);

            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }
    });

app.MapGet("/api/inventory/{productId}", async (
    string productId,
    IInventoryClient inventoryClient,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) => {
        ILogger logger = loggerFactory.CreateLogger("Orders.GetInventory");

        try {
            InventoryResponse inventory = await inventoryClient.GetAsync(productId, cancellationToken).ConfigureAwait(false);

            logger.InventoryRetrieved(inventory.ProductId, inventory.AvailableQuantity);

            return Results.Ok(inventory);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.InventoryRetrievalFailed(exception, productId);

            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }
    });

app.MapPost($"/api/orders", async (
    RunScenarioRequest request,
    OrdersDbContext db,
    IInventoryClient inventoryClient,
    IPaymentsClient paymentsClient,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) => {
        ILogger logger = loggerFactory.CreateLogger("Orders.PlaceOrder");

        logger.PlacingOrder(
            request.OrderId,
            request.CustomerId,
            request.ProductId,
            request.Quantity);

        try {
            OrderRecord? existing = await db.Orders.AsNoTracking().SingleOrDefaultAsync(
                order => order.IdempotencyKey == request.IdempotencyKey,
                cancellationToken).ConfigureAwait(false);
            if (existing is not null) {
                logger.OrderCompletedWithStatus(existing.OrderId, existing.Status);

                return Results.Ok(ToResponse(existing));
            }

            var reservationId = Guid.NewGuid();
            var order = new OrderRecord {
                OrderId = request.OrderId,
                IdempotencyKey = request.IdempotencyKey,
                CustomerId = request.CustomerId,
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                ReservationId = reservationId,
                Status = OrderStatus.Created.ToString(),
            };

            db.Orders.Add(order);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            ReserveInventoryResponse reservation = await inventoryClient.ReserveAsync(
                request.ProductId,
                new ReserveInventoryRequest(reservationId, request.OrderId, request.Quantity),
                cancellationToken).ConfigureAwait(false);

            if (!reservation.Reserved) {
                order.Status = OrderStatus.Rejected.ToString();
                order.Reason = reservation.Reason;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                logger.OrderCompletedWithStatus(order.OrderId, order.Status);

                return Results.Ok(ToResponse(order));
            }

            order.Status = OrderStatus.InventoryReserved.ToString();
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            AuthorizePaymentResponse payment = await paymentsClient.AuthorizeAsync(
                new AuthorizePaymentRequest(
                    Guid.NewGuid(),
                    request.OrderId,
                    request.CustomerId,
                    request.IdempotencyKey,
                    request.SimulatePaymentFailure),
                cancellationToken).ConfigureAwait(false);

            if (!payment.Authorized) {
                await inventoryClient.ReleaseAsync(
                    request.ProductId,
                    new ReleaseInventoryRequest(reservationId),
                    cancellationToken).ConfigureAwait(false);

                order.Status = OrderStatus.Rejected.ToString();
                order.Reason = payment.Reason;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                logger.OrderCompletedWithStatus(order.OrderId, order.Status);

                return Results.Ok(ToResponse(order));
            }

            order.Status = OrderStatus.Completed.ToString();
            order.Reason = null;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            logger.OrderCompletedWithStatus(order.OrderId, order.Status);

            return Results.Ok(ToResponse(order));
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.OrderPlacementFailed(
                exception,
                request.OrderId,
                request.ProductId,
                request.Quantity);

            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }
    });

app.MapGet("/api/orders/{orderId:guid}", async (
    Guid orderId,
    OrdersDbContext db,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) => {
        ILogger logger = loggerFactory.CreateLogger("Orders.GetOrder");

        try {
            OrderRecord? order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(
                order => order.OrderId == orderId,
                cancellationToken).ConfigureAwait(false);

            if (order is null) {
                logger.OrderNotFound(orderId);

                return Results.NotFound();
            }

            logger.OrderRetrievedWithStatus(order.OrderId, order.Status);

            return Results.Ok(ToResponse(order));
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.OrderRetrievalFailed(exception, orderId);

            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }
    });

// Map the shared health and aliveness endpoints.
app.MapDefaultEndpoints();

await app.RunAsync().ConfigureAwait(false);

static OrderResponse ToResponse(OrderRecord order) {
    return new OrderResponse(
        order.OrderId,
        Enum.Parse<OrderStatus>(order.Status),
        order.Reason);
}

static async Task EnsureDatabaseAsync(IServiceProvider services) {
    using IServiceScope scope = services.CreateScope();
    OrdersDbContext db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
}

public partial class Program;
