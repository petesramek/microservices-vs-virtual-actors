using ArchitectureComparison.Contracts;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Clients;
using Orders.Api.Data;
using Orders.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddDbContext<OrdersDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=orders.db";
    options.UseSqlite(connectionString);
});

builder.Services.AddHttpClient<IInventoryClient, HttpInventoryClient>(client =>
{
    var baseUrl = builder.Configuration["Services:InventoryBaseUrl"] ?? "http://localhost:5201";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddHttpClient<IPaymentsClient, HttpPaymentsClient>(client =>
{
    var baseUrl = builder.Configuration["Services:PaymentsBaseUrl"] ?? "http://localhost:5202";
    client.BaseAddress = new Uri(baseUrl);
});

var app = builder.Build();

await EnsureDatabaseAsync(app.Services);

app.MapGet("/", () => Results.Ok(new { Name = "Orders API", Phase = "Microservices" }));
app.MapGet("/health/live", () => Results.Ok("Healthy"));

app.MapPost("/api/scenarios/reset", async (ResetInventoryRequest request, IInventoryClient inventoryClient, OrdersDbContext db, CancellationToken cancellationToken) =>
{
    db.Orders.RemoveRange(await db.Orders.ToListAsync(cancellationToken));
    await db.SaveChangesAsync(cancellationToken);

    var inventory = await inventoryClient.ResetAsync(request, cancellationToken);
    return Results.Ok(inventory);
});

app.MapGet("/api/inventory/{productId}", async (string productId, IInventoryClient inventoryClient, CancellationToken cancellationToken) =>
{
    return Results.Ok(await inventoryClient.GetAsync(productId, cancellationToken));
});

app.MapPost("/api/orders", async (RunScenarioRequest request, OrdersDbContext db, IInventoryClient inventoryClient, IPaymentsClient paymentsClient, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
{
    var existing = await db.Orders.AsNoTracking().SingleOrDefaultAsync(order => order.IdempotencyKey == request.IdempotencyKey, cancellationToken);
    if (existing is not null)
    {
        return Results.Ok(ToResponse(existing));
    }

    var logger = loggerFactory.CreateLogger("Orders.PlaceOrder");
    var reservationId = Guid.NewGuid();

    var order = new OrderRecord
    {
        OrderId = request.OrderId,
        IdempotencyKey = request.IdempotencyKey,
        CustomerId = request.CustomerId,
        ProductId = request.ProductId,
        Quantity = request.Quantity,
        ReservationId = reservationId,
        Status = OrderStatus.Created.ToString()
    };

    db.Orders.Add(order);
    await db.SaveChangesAsync(cancellationToken);

    var reservation = await inventoryClient.ReserveAsync(
        request.ProductId,
        new ReserveInventoryRequest(reservationId, request.OrderId, request.Quantity),
        cancellationToken);

    if (!reservation.Reserved)
    {
        order.Status = OrderStatus.Rejected.ToString();
        order.Reason = reservation.Reason;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(order));
    }

    order.Status = OrderStatus.InventoryReserved.ToString();
    await db.SaveChangesAsync(cancellationToken);

    var payment = await paymentsClient.AuthorizeAsync(
        new AuthorizePaymentRequest(Guid.NewGuid(), request.OrderId, request.CustomerId, request.IdempotencyKey, request.SimulatePaymentFailure),
        cancellationToken);

    if (!payment.Authorized)
    {
        await inventoryClient.ReleaseAsync(request.ProductId, new ReleaseInventoryRequest(reservationId), cancellationToken);
        order.Status = OrderStatus.Rejected.ToString();
        order.Reason = payment.Reason;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Rejected order {OrderId} because payment failed", order.OrderId);
        return Results.Ok(ToResponse(order));
    }

    order.Status = OrderStatus.Completed.ToString();
    order.Reason = null;
    await db.SaveChangesAsync(cancellationToken);

    logger.LogInformation("Completed order {OrderId}", order.OrderId);
    return Results.Ok(ToResponse(order));
});

app.MapGet("/api/orders/{orderId:guid}", async (Guid orderId, OrdersDbContext db, CancellationToken cancellationToken) =>
{
    var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
    return order is null ? Results.NotFound() : Results.Ok(ToResponse(order));
});

app.Run();

static OrderResponse ToResponse(OrderRecord order)
{
    return new OrderResponse(
        order.OrderId,
        Enum.Parse<OrderStatus>(order.Status),
        order.Reason);
}

static async Task EnsureDatabaseAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    await db.Database.EnsureCreatedAsync();
}

public partial class Program;
