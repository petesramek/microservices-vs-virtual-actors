using Comparison.Contracts;
using Microsoft.EntityFrameworkCore;
using Payments.Api.Data;
using Payments.Api.Logging;
using Payments.Api.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddDbContext<PaymentsDbContext>(options => {
    var connectionString = builder.Configuration.GetConnectionString($"Default") ?? $"Data Source=payments.db";
    options.UseSqlite(connectionString);
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

await EnsureDatabaseAsync(app.Services).ConfigureAwait(false);

app.MapGet($"/", () => Results.Ok(new { Name = $"Payments API", Phase = $"Microservices" }));
app.MapGet($"/health/live", () => Results.Ok($"Healthy"));

app.MapPost($"/api/payments/authorize", async (
    AuthorizePaymentRequest request,
    PaymentsDbContext db,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) => {
    ILogger logger = loggerFactory.CreateLogger("Payments.Authorize");

    logger.AuthorizingPayment(
        request.PaymentId,
        request.OrderId,
        request.CustomerId);

    try {
        PaymentAttempt? existing = await db.PaymentAttempts.AsNoTracking().SingleOrDefaultAsync(
            paymentAttempt => paymentAttempt.IdempotencyKey == request.IdempotencyKey,
            cancellationToken).ConfigureAwait(false);

        if (existing is not null) {
            logger.PaymentAuthorizationCompleted(
                existing.PaymentId,
                existing.OrderId,
                existing.Authorized);

            return Results.Ok(new AuthorizePaymentResponse(existing.Authorized, existing.Reason));
        }

        var authorized = !request.SimulateFailure;
        var reason = authorized ? null : $"PaymentFailed";

        db.PaymentAttempts.Add(new PaymentAttempt {
            PaymentId = request.PaymentId,
            OrderId = request.OrderId,
            CustomerId = request.CustomerId,
            IdempotencyKey = request.IdempotencyKey,
            Authorized = authorized,
            Reason = reason,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.PaymentAuthorizationCompleted(
            request.PaymentId,
            request.OrderId,
            authorized);

        return Results.Ok(new AuthorizePaymentResponse(authorized, reason));
    }
    catch (OperationCanceledException) {
        throw;
    }
    catch (Exception exception) {
        logger.PaymentAuthorizationFailed(
            exception,
            request.PaymentId,
            request.OrderId,
            request.CustomerId);

        return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
    }
});

await app.RunAsync().ConfigureAwait(false);

static async Task EnsureDatabaseAsync(IServiceProvider services) {
    using IServiceScope scope = services.CreateScope();
    PaymentsDbContext db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
}

public partial class Program;
