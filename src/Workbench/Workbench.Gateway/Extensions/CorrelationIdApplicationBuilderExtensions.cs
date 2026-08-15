namespace Workbench.Gateway.Extensions;

/// <summary>
/// Provides extensions for configuring correlation identifier handling.
/// </summary>
internal static class CorrelationIdApplicationBuilderExtensions {
    private const string CorrelationIdHeader = "X-Correlation-ID";

    /// <summary>
    /// Adds correlation identifier handling to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) {
        ArgumentNullException.ThrowIfNull(app);

        app.Use(async (context, next) => {
            string? correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(correlationId)) {
                correlationId = $"run-{Guid.NewGuid():N}";
            }

            context.Response.Headers[CorrelationIdHeader] = CorrelationIdContext.CurrentId = correlationId;

            ILogger logger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Workbench.Gateway");

            using IDisposable? scope = logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal) {
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
    /// Provides access to the correlation identifier for the current asynchronous execution flow.
    /// </summary>
    internal static class CorrelationIdContext {
        private static readonly AsyncLocal<string?> Current = new();

        /// <summary>
        /// Gets or sets the correlation identifier for the current asynchronous execution flow.
        /// </summary>
        public static string? CurrentId {
            get => Current.Value;
            set => Current.Value = value;
        }
    }

}
