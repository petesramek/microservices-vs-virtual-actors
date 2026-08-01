using Comparison.Contracts;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Clients;
using Orders.Api.Clients.Abstraction;
using Orders.Api.Data;
using Orders.Api.Models;
using System.Collections.Concurrent;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddDbContext<OrdersDbContext>(options => {
    var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=orders.db";
    options.UseSqlite(connectionString);
});

builder.Services.AddHttpClient<IInventoryClient, HttpInventoryClient>(client => {
    var baseUrl = builder.Configuration["Services:InventoryBaseUrl"] ?? "http://localhost:5201";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddHttpClient<IPaymentsClient, HttpPaymentsClient>(client => {
    var baseUrl = builder.Configuration["Services:PaymentsBaseUrl"] ?? "http://localhost:5202";
    client.BaseAddress = new Uri(baseUrl);
});

var app = builder.Build();
// correlation-id-logging
app.Use(async (context, next) => {
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId)) {
        await next().ConfigureAwait(false);
        return;
    }

    using var scope = app.Logger.BeginScope(new Dictionary<string, object> {
        ["CorrelationId"] = correlationId,
    });

    app.Logger.LogInformation("Handling request with correlation id {CorrelationId}.", correlationId);
    await next().ConfigureAwait(false);
});

// orders-api-idempotency-keyed-gate
var orderIdempotencyLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

app.Use(async (context, next) => {
    if (!HttpMethods.IsPost(context.Request.Method) || !context.Request.Path.Equals("/api/orders")) {
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

    var requestLock = orderIdempotencyLocks.GetOrAdd(request.IdempotencyKey, _ => new SemaphoreSlim(1, 1));
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

app.MapGet("/", () => Results.Ok(new { Name = "Orders API", Phase = "Microservices" }));
app.MapGet("/health/live", () => Results.Ok("Healthy"));

app.MapPost("/api/scenarios/reset", async (ResetInventoryRequest request, IInventoryClient inventoryClient, OrdersDbContext db, CancellationToken cancellationToken) => {
    db.Orders.RemoveRange(await db.Orders.ToListAsync(cancellationToken));
    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    var inventory = await inventoryClient.ResetAsync(request, cancellationToken).ConfigureAwait(false);
    return Results.Ok(inventory);
});

app.MapGet("/api/inventory/{productId}", async (string productId, IInventoryClient inventoryClient, CancellationToken cancellationToken) => {
    return Results.Ok(await inventoryClient.GetAsync(productId, cancellationToken));
});

app.MapPost("/api/orders", async (RunScenarioRequest request, OrdersDbContext db, IInventoryClient inventoryClient, IPaymentsClient paymentsClient, ILoggerFactory loggerFactory, CancellationToken cancellationToken) => {
    var existing = await db.Orders.AsNoTracking().SingleOrDefaultAsync(order => order.IdempotencyKey == request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
    if (existing is not null) {
        return Results.Ok(ToResponse(existing));
    }

    var logger = loggerFactory.CreateLogger("Orders.PlaceOrder");
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

    var reservation = await inventoryClient.ReserveAsync(
        request.ProductId,
        new ReserveInventoryRequest(reservationId, request.OrderId, request.Quantity),
        cancellationToken).ConfigureAwait(false);

    if (!reservation.Reserved) {
        order.Status = OrderStatus.Rejected.ToString();
        order.Reason = reservation.Reason;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Results.Ok(ToResponse(order));
    }

    order.Status = OrderStatus.InventoryReserved.ToString();
    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    var payment = await paymentsClient.AuthorizeAsync(
        new AuthorizePaymentRequest(Guid.NewGuid(), request.OrderId, request.CustomerId, request.IdempotencyKey, request.SimulatePaymentFailure),
        cancellationToken).ConfigureAwait(false);

    if (!payment.Authorized) {
        await inventoryClient.ReleaseAsync(request.ProductId, new ReleaseInventoryRequest(reservationId), cancellationToken).ConfigureAwait(false);
        order.Status = OrderStatus.Rejected.ToString();
        order.Reason = payment.Reason;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Rejected order {OrderId} because payment failed", order.OrderId);
        return Results.Ok(ToResponse(order));
    }

    order.Status = OrderStatus.Completed.ToString();
    order.Reason = null;
    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    logger.LogInformation("Completed order {OrderId}", order.OrderId);
    return Results.Ok(ToResponse(order));
});

app.MapGet("/api/orders/{orderId:guid}", async (Guid orderId, OrdersDbContext db, CancellationToken cancellationToken) => {
    var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken).ConfigureAwait(false);
    return order is null ? Results.NotFound() : Results.Ok(ToResponse(order));
});

app.Run();

static OrderResponse ToResponse(OrderRecord order) {
    return new OrderResponse(
        order.OrderId,
        Enum.Parse<OrderStatus>(order.Status),
        order.Reason);
}

static async Task EnsureDatabaseAsync(IServiceProvider services) {
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
}

public partial class Program;


