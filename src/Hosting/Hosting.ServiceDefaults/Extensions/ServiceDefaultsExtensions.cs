namespace Hosting.ServiceDefaults.Extensions;

using global::Observability.Health;
using Hosting.ServiceDefaults.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Text.Json;
using System.Text.Json.Serialization;
using FrameworkHealthCheckOptions = Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions;
using FrameworkHealthCheckResult = Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult;
using FrameworkHealthReport = Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport;
using FrameworkHealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

/// <summary>
/// Provides shared hosting defaults for application services.
/// </summary>
public static class ServiceDefaultsExtensions {
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";
    private const string ScenarioEndpointPath = "/api/scenarios/run";
    private const string OrleansMeterName = "Microsoft.Orleans";
    private const string OrleansActivitySourceName = "Microsoft.Orleans.*";
    private const string EntityFrameworkCoreMeterName = "Microsoft.EntityFrameworkCore";

    private static readonly JsonSerializerOptions HealthJsonSerializerOptions =
        new(JsonSerializerDefaults.Web) {
            Converters = {
                new JsonStringEnumConverter(),
            },
        };

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
            .Validate(
                options => Enum.IsDefined(options.TraceSources),
                "The configured observability trace sources is not supported.")
            .Validate(
                options => Enum.IsDefined(options.MetricSources),
                "The configured observability metric sources is not supported.")
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
                if (observabilityOptions.MetricSources.HasFlag(MetricSource.AspNetCore)) {
                    metrics
                        .AddAspNetCoreInstrumentation();
                }

                if (observabilityOptions.MetricSources.HasFlag(MetricSource.HttpClient)) {
                    metrics
                        .AddHttpClientInstrumentation();
                }

                if (observabilityOptions.MetricSources.HasFlag(MetricSource.Runtime)) {
                    metrics
                        .AddRuntimeInstrumentation();
                }

                if (observabilityOptions.MetricSources.HasFlag(MetricSource.EntityFrameworkCore)) {
                    metrics
                        .AddMeter(EntityFrameworkCoreMeterName);
                }

                if (observabilityOptions.MetricSources.HasFlag(MetricSource.MicrosoftOrleans)) {
                    metrics
                        .AddMeter(OrleansMeterName);
                }

                if (observabilityOptions.MetricSources.HasFlag(MetricSource.Scenario)) {
                    metrics
                        .AddMeter(ScenarioMetrics.MeterName);
                }
            })
            .WithTracing(tracing => {
                if (observabilityOptions.TraceMode == TraceCollectionMode.ScenarioOnly) {
                    tracing.SetSampler(
                        new ParentBasedSampler(
                            new ScenarioTraceSampler()));
                }

                tracing
                    .AddSource(builder.Environment.ApplicationName);

                if (observabilityOptions.TraceSources.HasFlag(TraceSource.Scenario)) {
                    tracing
                        .AddSource(ScenarioTelemetry.ActivitySourceName);
                }

                if (observabilityOptions.TraceSources.HasFlag(TraceSource.MicrosoftOrleans)) {
                    tracing
                        .AddSource(OrleansActivitySourceName);
                }

                if (observabilityOptions.TraceSources.HasFlag(TraceSource.AspNetCore)) {
                    tracing
                        .AddAspNetCoreInstrumentation(options => {
                            options.Filter = context =>
                                ShouldTraceServerRequest(
                                    context,
                                    observabilityOptions.TraceMode);
                        });
                }

                if (observabilityOptions.TraceSources.HasFlag(TraceSource.HttpClient)) {
                    tracing
                        .AddHttpClientInstrumentation(options => {
                            options.FilterHttpRequestMessage = request =>
                                ShouldTraceClientRequest(
                                    request,
                                    observabilityOptions.TraceMode);
                        });
                }

                if (observabilityOptions.TraceSources.HasFlag(TraceSource.EntityFrameworkCore)) {
                    tracing
                        .AddEntityFrameworkCoreInstrumentation();
                }
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
                () => FrameworkHealthCheckResult.Healthy(),
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
            app.MapHealthChecks(
                HealthEndpointPath,
                new FrameworkHealthCheckOptions {
                    ResponseWriter = WriteHealthReportAsync,
                });

            app.MapHealthChecks(
                AlivenessEndpointPath,
                new FrameworkHealthCheckOptions {
                    Predicate = registration =>
                        registration.Tags.Contains("live"),
                });
        }

        return app;
    }

    private static Task WriteHealthReportAsync(
        HttpContext context,
        FrameworkHealthReport report) {
        HealthReport response = new(
            MapHealthStatus(report.Status),
            ToMilliseconds(report.TotalDuration),
            report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new HealthEntry(
                    MapHealthStatus(entry.Value.Status),
                    entry.Value.Description,
                    ToMilliseconds(entry.Value.Duration)),
                StringComparer.Ordinal));

        return context.Response.WriteAsJsonAsync(
            response,
            HealthJsonSerializerOptions,
            context.RequestAborted);
    }

    private static HealthStatus MapHealthStatus(
        FrameworkHealthStatus status) {
        return status switch {
            FrameworkHealthStatus.Healthy => HealthStatus.Healthy,
            FrameworkHealthStatus.Degraded => HealthStatus.Degraded,
            FrameworkHealthStatus.Unhealthy => HealthStatus.Unhealthy,
            _ => HealthStatus.Unknown,
        };
    }

    private static long ToMilliseconds(TimeSpan duration) {
        return (long)Math.Ceiling(duration.TotalMilliseconds);
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
                ScenarioTelemetry.ScenarioHeaderName);
    }

    private static bool ShouldTraceClientRequest(
        HttpRequestMessage request,
        TraceCollectionMode traceMode) {
        return traceMode == TraceCollectionMode.Full
            || request.Headers.Contains(
                ScenarioTelemetry.ScenarioHeaderName);
    }

    private static bool IsHealthRequest(PathString requestPath) {
        return requestPath.StartsWithSegments(HealthEndpointPath)
            || requestPath.StartsWithSegments(AlivenessEndpointPath);
    }
}
