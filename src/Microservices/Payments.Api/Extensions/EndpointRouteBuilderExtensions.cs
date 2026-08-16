namespace Payments.Api.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Payments.Api.Internal.Infrastructure;
using Payments.Api.Internal.Observability.Logging;
using Payments.Api.Models;
using Workbench.Contracts;
using Workbench.Contracts.Payments;

/// <summary>
/// Provides endpoint-registration methods for the Payments API.
/// </summary>
internal static class EndpointRouteBuilderExtensions {
    /// <summary>
    /// Identifies the logger category used by payment authorization endpoints.
    /// </summary>
    private const string AuthorizationLoggerCategory = "Payments.Authorize";

    /// <summary>
    /// Identifies the reason returned when simulated payment authorization fails.
    /// </summary>
    private const string PaymentFailureReason = "PaymentFailed";

    /// <summary>
    /// Maps the Payments API root and authorization endpoints.
    /// </summary>
    /// <param name="endpoints">
    /// The endpoint route builder that receives the payment routes.
    /// </param>
    /// <returns>
    /// <paramref name="endpoints"/> so additional endpoints can be mapped.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="endpoints"/> is <see langword="null"/>.
    /// </exception>
    public static IEndpointRouteBuilder MapPaymentsEndpoints(
        this IEndpointRouteBuilder endpoints) {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/", GetServiceInformation);

        RouteGroupBuilder payments = endpoints.MapGroup("/api/payments");
        payments.MapPost("/authorize", AuthorizePaymentAsync);

        return endpoints;
    }

    /// <summary>
    /// Returns identifying information for the Payments API.
    /// </summary>
    /// <returns>An HTTP 200 response containing the service name and phase.</returns>
    private static IResult GetServiceInformation() {
        return Results.Ok(new {
            Name = "Payments API",
            Phase = "Microservices",
        });
    }

    /// <summary>
    /// Authorizes a payment request and persists its idempotent outcome.
    /// </summary>
    /// <param name="request">The payment authorization request.</param>
    /// <param name="db">The payments database context.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="cancellationToken">
    /// The token that cancels request processing and database operations.
    /// </param>
    /// <returns>
    /// An HTTP 200 response containing the authorization result, or HTTP 500
    /// when an unexpected failure occurs.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled while the request is
    /// being processed.
    /// </exception>
    private static async Task<IResult> AuthorizePaymentAsync(
        AuthorizePaymentRequest request,
        PaymentsDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) {
        ILogger logger =
            loggerFactory.CreateLogger(AuthorizationLoggerCategory);

        logger.AuthorizingPayment(
            request.PaymentId,
            request.OrderId,
            request.CustomerId);

        try {
            PaymentAttempt? existing = await db.PaymentAttempts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    paymentAttempt =>
                        paymentAttempt.IdempotencyKey
                        == request.IdempotencyKey,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null) {
                logger.PaymentAuthorizationCompleted(
                    existing.PaymentId,
                    existing.OrderId,
                    existing.Authorized);

                return Results.Ok(new AuthorizePaymentResponse(
                    existing.Authorized,
                    existing.Reason));
            }

            bool authorized = !request.SimulateFailure;
            string? reason = authorized ? null : PaymentFailureReason;

            db.PaymentAttempts.Add(new PaymentAttempt {
                PaymentId = request.PaymentId,
                OrderId = request.OrderId,
                CustomerId = request.CustomerId,
                IdempotencyKey = request.IdempotencyKey,
                Authorized = authorized,
                Reason = reason,
            });

            await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);

            logger.PaymentAuthorizationCompleted(
                request.PaymentId,
                request.OrderId,
                authorized);

            return Results.Ok(new AuthorizePaymentResponse(
                authorized,
                reason));
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.PaymentAuthorizationFailed(
                exception,
                request.PaymentId,
                request.OrderId,
                request.CustomerId);

            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
