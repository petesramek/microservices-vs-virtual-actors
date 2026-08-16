namespace Orders.Api.Extensions;

using Orders.Api.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using Workbench.Contracts;

/// <summary>
/// Provides request-pipeline configuration for the Orders API.
/// </summary>
internal static class OrdersApplicationBuilderExtensions {
    /// <summary>
    /// Adds a logging scope for requests that provide a correlation identifier.
    /// </summary>
    /// <param name="app">
    /// The web application whose request pipeline is configured.
    /// </param>
    /// <returns>The supplied web application.</returns>
    internal static WebApplication UseCorrelationLogging(this WebApplication app) {
        app.Use(async (context, next) => {
            string? correlationId = context.Request.Headers["X-Correlation-ID"]
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(correlationId)) {
                await next().ConfigureAwait(false);
                return;
            }

            using IDisposable? scope = app.Logger.BeginScope(
                new Dictionary<string, object>(StringComparer.Ordinal) {
                    ["CorrelationId"] = correlationId,
                });

            app.Logger.HandlingRequestWithCorrelationId(correlationId);
            await next().ConfigureAwait(false);
        });

        return app;
    }

    /// <summary>
    /// Serializes concurrent order-placement requests that share an idempotency
    /// key within the current application process.
    /// </summary>
    /// <param name="app">
    /// The web application whose request pipeline is configured.
    /// </param>
    /// <returns>The supplied web application.</returns>
    internal static WebApplication UseOrderIdempotencyGate(this WebApplication app) {
        ConcurrentDictionary<string, SemaphoreSlim> orderIdempotencyLocks =
            new(StringComparer.Ordinal);
        JsonSerializerOptions serializerOptions =
            new(JsonSerializerDefaults.Web);

        app.Use(async (context, next) => {
            if (!HttpMethods.IsPost(context.Request.Method)
                || !context.Request.Path.Equals(
                    "/api/orders",
                    StringComparison.OrdinalIgnoreCase)) {
                await next().ConfigureAwait(false);
                return;
            }

            context.Request.EnableBuffering();
            RunScenarioRequest? request = null;

            try {
                request = await JsonSerializer
                    .DeserializeAsync<RunScenarioRequest>(
                        context.Request.Body,
                        serializerOptions,
                        context.RequestAborted)
                    .ConfigureAwait(false);
            } finally {
                context.Request.Body.Position = 0;
            }

            if (string.IsNullOrWhiteSpace(request?.IdempotencyKey)) {
                await next().ConfigureAwait(false);
                return;
            }

            SemaphoreSlim requestLock = orderIdempotencyLocks.GetOrAdd(
                request.IdempotencyKey,
                static _ => new SemaphoreSlim(1, 1));

            await requestLock
                .WaitAsync(context.RequestAborted)
                .ConfigureAwait(false);

            try {
                await next().ConfigureAwait(false);
            } finally {
                requestLock.Release();

                if (requestLock.CurrentCount == 1) {
                    orderIdempotencyLocks.TryRemove(
                        request.IdempotencyKey,
                        out _);
                }
            }
        });

        return app;
    }
}
