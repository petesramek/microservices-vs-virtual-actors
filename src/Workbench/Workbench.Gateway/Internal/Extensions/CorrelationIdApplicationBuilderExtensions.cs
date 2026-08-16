namespace Workbench.Gateway.Internal.Extensions;

/// <summary>
/// Provides application-pipeline extensions for correlation identifier
/// handling.
/// </summary>
internal static class CorrelationIdApplicationBuilderExtensions {
    /// <summary>
    /// Identifies the HTTP request and response header that carries the
    /// correlation identifier.
    /// </summary>
    private const string CorrelationIdHeader = "X-Correlation-ID";

    /// <summary>
    /// Adds correlation identifier resolution, propagation, and logging scope
    /// creation to the application pipeline.
    /// </summary>
    /// <param name="app">
    /// The application builder that receives the correlation middleware.
    /// </param>
    /// <returns>
    /// <paramref name="app"/> so additional middleware can be configured.
    /// </returns>
    /// <remarks>
    /// The middleware reuses a non-blank request header when supplied. Otherwise,
    /// it generates a new identifier. The resolved value is written to the
    /// response header, placed in the current asynchronous execution context,
    /// and added to a structured logging scope for the remainder of the request.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="app"/> is <see langword="null"/>.
    /// </exception>
    public static IApplicationBuilder UseCorrelationId(
        this IApplicationBuilder app) {
        ArgumentNullException.ThrowIfNull(app);

        app.Use(async (context, next) => {
            string? correlationId = context.Request
                .Headers[CorrelationIdHeader]
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(correlationId)) {
                correlationId = $"run-{Guid.NewGuid():N}";
            }

            context.Response.Headers[CorrelationIdHeader] =
                CorrelationIdContext.CurrentId = correlationId;

            ILogger logger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Workbench.Gateway");

            using IDisposable? scope = logger.BeginScope(
                new Dictionary<string, object>(StringComparer.Ordinal) {
                    ["CorrelationId"] = correlationId,
                });

            try {
                await next(context).ConfigureAwait(false);
            } finally {
                CorrelationIdContext.CurrentId = null;
            }
        });

        return app;
    }

    /// <summary>
    /// Provides access to the correlation identifier associated with the current
    /// asynchronous execution flow.
    /// </summary>
    internal static class CorrelationIdContext {
        /// <summary>
        /// Stores the correlation identifier for the current asynchronous
        /// execution context.
        /// </summary>
        private static readonly AsyncLocal<string?> Current = new();

        /// <summary>
        /// Gets or sets the correlation identifier for the current asynchronous
        /// execution flow.
        /// </summary>
        /// <value>
        /// The current correlation identifier, or <see langword="null"/> when no
        /// correlation scope is active.
        /// </value>
        public static string? CurrentId {
            get => Current.Value;
            set => Current.Value = value;
        }
    }
}
