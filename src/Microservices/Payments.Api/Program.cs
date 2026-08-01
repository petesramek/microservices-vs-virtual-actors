using Comparison.Contracts;
using Microsoft.EntityFrameworkCore;
using Payments.Api.Data;
using Payments.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddDbContext<PaymentsDbContext>(options => {
    var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=payments.db";
    options.UseSqlite(connectionString);
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

    if (app.Logger.IsEnabled(LogLevel.Information)) {
        app.Logger.LogInformation("Handling request with correlation id {CorrelationId}.", correlationId);
    }
    await next().ConfigureAwait(false);
});

await EnsureDatabaseAsync(app.Services).ConfigureAwait(false);

app.MapGet("/", () => Results.Ok(new { Name = "Payments API", Phase = "Microservices" }));
app.MapGet("/health/live", () => Results.Ok("Healthy"));

app.MapPost("/api/payments/authorize", async (AuthorizePaymentRequest request, PaymentsDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken) => {
    var existing = await db.PaymentAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
    if (existing is not null) {
        return Results.Ok(new AuthorizePaymentResponse(existing.Authorized, existing.Reason));
    }

    var authorized = !request.SimulateFailure;
    var reason = authorized ? null : "PaymentFailed";

    db.PaymentAttempts.Add(new PaymentAttempt {
        PaymentId = request.PaymentId,
        OrderId = request.OrderId,
        CustomerId = request.CustomerId,
        IdempotencyKey = request.IdempotencyKey,
        Authorized = authorized,
        Reason = reason,
    });

    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    var logger = loggerFactory.CreateLogger("Payments.Authorize");
    if (logger.IsEnabled(LogLevel.Information)) {
        logger.LogInformation("Payment authorization for order {OrderId} completed with authorized={Authorized}", request.OrderId, authorized);
    }

    return Results.Ok(new AuthorizePaymentResponse(authorized, reason));
});

app.Run();

static async Task EnsureDatabaseAsync(IServiceProvider services) {
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
}

public partial class Program;

