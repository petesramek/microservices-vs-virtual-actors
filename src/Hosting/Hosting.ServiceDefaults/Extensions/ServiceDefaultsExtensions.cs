namespace Hosting.ServiceDefaults.Extensions;

using Hosting.ServiceDefaults.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

/// <summary>
/// Provides shared hosting defaults for application services.
/// </summary>
public static class ServiceDefaultsExtensions {
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";
    private const string ScenarioEndpointPath = "/api/scenarios/run";
    private const string ScenarioHeaderName = "X-Scenario-Run";
    private const string OrleansMeterName = "Microsoft.Orleans";
    private const string OrleansActivitySourceName = "Microsoft.Orleans.*";

    /// <summary>
    /// Adds shared service discovery, resilience, health checks, and OpenTelemetry configuration.
    /// </summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The host application builder.</returns>
    public static TBuilder AddServiceDefaults<TBuilder>(
        this TBuilder builder)
        where TBuilder : IHostApplicationBuilder {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddOptions<ObservabilityOptions>()
            .Bind(builder.Configuration.GetSection(
                ObservabilityOptions.SectionName))
            .Validate(
                options => Enum.IsDefined(options.TraceMode),
                "The configured observability trace mode is not supported.")
            .ValidateOnStart();

        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(httpClientBuilder => {
            httpClientBuilder.AddStandardResilienceHandler();
            httpClientBuilder.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>
    /// Configures shared OpenTelemetry logging, metrics, tracing, and exporters.
    /// </summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The host application builder.</returns>
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(
        this TBuilder builder)
        where TBuilder : IHostApplicationBuilder {
        ArgumentNullException.ThrowIfNull(builder);

        ObservabilityOptions observabilityOptions =
            builder.Configuration
                .GetSection(ObservabilityOptions.SectionName)
                .Get<ObservabilityOptions>()
            ?? new ObservabilityOptions();

        builder.Logging.AddOpenTelemetry(logging => {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services
            .AddOpenTelemetry()
            .WithMetrics(metrics => {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(OrleansMeterName);
            })
            .WithTracing(tracing => {
                tracing
                    .AddSource(builder.Environment.ApplicationName)
                    .AddSource(OrleansActivitySourceName)
                    .AddAspNetCoreInstrumentation(options => {
                        options.Filter = context =>
                            ShouldTraceServerRequest(
                                context,
                                observabilityOptions.TraceMode);
                    })
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    /// <summary>
    /// Adds the default application health checks.
    /// </summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The host application builder.</returns>
    public static TBuilder AddDefaultHealthChecks<TBuilder>(
        this TBuilder builder)
        where TBuilder : IHostApplicationBuilder {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy(),
                ["live"]);

        return builder;
    }

    /// <summary>
    /// Maps the default health and aliveness endpoints in development environments.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application.</returns>
    public static WebApplication MapDefaultEndpoints(
        this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        if (app.Environment.IsDevelopment()) {
            app.MapHealthChecks(HealthEndpointPath);

            app.MapHealthChecks(
                AlivenessEndpointPath,
                new HealthCheckOptions {
                    Predicate = registration =>
                        registration.Tags.Contains("live"),
                });
        }

        return app;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(
        this TBuilder builder)
        where TBuilder : IHostApplicationBuilder {
        if (!string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])) {
            builder.Services
                .AddOpenTelemetry()
                .UseOtlpExporter();
        }

        return builder;
    }

    private static bool ShouldTraceServerRequest(
        HttpContext context,
        TraceCollectionMode traceMode) {
        if (IsHealthRequest(context.Request.Path)) {
            return false;
        }

        if (traceMode == TraceCollectionMode.Full) {
            return true;
        }

        return context.Request.Path.StartsWithSegments(
                ScenarioEndpointPath)
            || context.Request.Headers.ContainsKey(
                ScenarioHeaderName);
    }

    private static bool IsHealthRequest(PathString requestPath) {
        return requestPath.StartsWithSegments(HealthEndpointPath)
            || requestPath.StartsWithSegments(AlivenessEndpointPath);
    }
}
