namespace Hosting.ServiceDefaults.Extensions;

using global::Observability.Health;
using Hosting.ServiceDefaults.Observability;
using Hosting.ServiceDefaults.Observability.Configuration;
using Hosting.ServiceDefaults.Observability.Tracing;
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
/// Provides shared hosting, health, service-discovery, resilience, and
/// observability defaults for application services.
/// </summary>
/// <remarks>
/// The extensions configure OpenTelemetry from the <c>Observability</c>
/// configuration section, register a tagged self health check, and map the
/// shared health endpoints. Export through OTLP is enabled only when
/// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is configured.
/// </remarks>
public static class ServiceDefaultsExtensions {
    /// <summary>
    /// Defines the JSON serialization contract for readiness health reports.
    /// </summary>
    /// <remarks>
    /// The web defaults use camel-case property names, and enum values are
    /// serialized as strings.
    /// </remarks>
    private static readonly JsonSerializerOptions HealthJsonSerializerOptions =
        new(JsonSerializerDefaults.Web) {
            Converters = {
                new JsonStringEnumConverter(),
            },
        };

    /// <summary>
    /// Adds the shared service discovery, HTTP resilience, health-check, and
    /// OpenTelemetry defaults to an application builder.
    /// </summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The host application builder to configure.</param>
    /// <returns>The supplied host application builder.</returns>
    /// <remarks>
    /// Observability options are bound from
    /// <see cref="ObservabilityOptions.SectionName"/> and validated when the
    /// application starts. The method also applies service discovery and the
    /// standard resilience handler to subsequently registered HTTP clients.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
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
                options => HasOnlyDefinedFlags(
                    options.TraceSources,
                    TraceSource.All),
                "The configured observability trace sources contain unsupported flags.")
            .Validate(
                options => HasOnlyDefinedFlags(
                    options.MetricSources,
                    MetricSource.All),
                "The configured observability metric sources contain unsupported flags.")
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
    /// Configures shared OpenTelemetry logging, metrics, tracing, sampling, and
    /// exporters.
    /// </summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The host application builder to configure.</param>
    /// <returns>The supplied host application builder.</returns>
    /// <remarks>
    /// Instrumentation is selected from <see cref="ObservabilityOptions"/>.
    /// Health requests are excluded from ASP.NET Core tracing. In scenario-only
    /// mode, server and client spans are retained only for scenario traffic and
    /// its propagated trace context. OTLP export is enabled separately when its
    /// endpoint environment variable is configured.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
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
            .WithMetrics(metrics =>
                ConfigureMetrics(
                    metrics,
                    observabilityOptions.MetricSources))
            .WithTracing(tracing =>
                ConfigureTracing(
                    tracing,
                    observabilityOptions,
                    builder.Environment.ApplicationName));

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    /// <summary>
    /// Adds metric instrumentation and meters selected by configuration.
    /// </summary>
    /// <param name="metrics">The OpenTelemetry metrics builder.</param>
    /// <param name="metricSources">The configured metric-source flags.</param>
    private static void ConfigureMetrics(
        MeterProviderBuilder metrics,
        MetricSource metricSources) {
        if (metricSources.HasFlag(MetricSource.AspNetCore)) {
            metrics.AddAspNetCoreInstrumentation();
        }

        if (metricSources.HasFlag(MetricSource.HttpClient)) {
            metrics.AddHttpClientInstrumentation();
        }

        if (metricSources.HasFlag(MetricSource.Runtime)) {
            metrics.AddRuntimeInstrumentation();
        }

        if (metricSources.HasFlag(MetricSource.EntityFrameworkCore)) {
            metrics.AddMeter(InstrumentationNames.EntityFrameworkCoreMeter);
        }

        if (metricSources.HasFlag(MetricSource.MicrosoftOrleans)) {
            metrics.AddMeter(InstrumentationNames.OrleansMeter);
        }

        if (metricSources.HasFlag(MetricSource.Scenario)) {
            metrics.AddMeter(ScenarioInstrumentation.MeterName);
        }
    }

    /// <summary>
    /// Adds tracing sources, instrumentation, filters, and sampling selected by
    /// configuration.
    /// </summary>
    /// <param name="tracing">The OpenTelemetry tracing builder.</param>
    /// <param name="options">The configured observability options.</param>
    /// <param name="applicationName">
    /// The activity-source name associated with the current application.
    /// </param>
    private static void ConfigureTracing(
        TracerProviderBuilder tracing,
        ObservabilityOptions options,
        string applicationName) {
        if (options.TraceMode == TraceCollectionMode.ScenarioOnly) {
            tracing.SetSampler(
                new ParentBasedSampler(
                    new ScenarioTraceSampler()));
        }

        tracing.AddSource(applicationName);

        if (options.TraceSources.HasFlag(TraceSource.Scenario)) {
            tracing.AddSource(ScenarioInstrumentation.ActivitySourceName);
        }

        if (options.TraceSources.HasFlag(TraceSource.MicrosoftOrleans)) {
            tracing.AddSource(InstrumentationNames.OrleansActivitySource);
        }

        if (options.TraceSources.HasFlag(TraceSource.AspNetCore)) {
            tracing.AddAspNetCoreInstrumentation(instrumentation => {
                instrumentation.Filter = context =>
                    ShouldTraceServerRequest(
                        context,
                        options.TraceMode);
            });
        }

        if (options.TraceSources.HasFlag(TraceSource.HttpClient)) {
            tracing.AddHttpClientInstrumentation(instrumentation => {
                instrumentation.FilterHttpRequestMessage = request =>
                    ShouldTraceClientRequest(
                        request,
                        options.TraceMode);
            });
        }

        if (options.TraceSources.HasFlag(TraceSource.EntityFrameworkCore)) {
            tracing.AddEntityFrameworkCoreInstrumentation();
        }
    }

    /// <summary>
    /// Adds the default application health checks.
    /// </summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The host application builder to configure.</param>
    /// <returns>The supplied host application builder.</returns>
    /// <remarks>
    /// Registers an always-healthy <c>self</c> check tagged with <c>live</c>.
    /// The tag allows the liveness endpoint to exclude dependency checks.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    public static TBuilder AddDefaultHealthChecks<TBuilder>(
        this TBuilder builder)
        where TBuilder : IHostApplicationBuilder {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddHealthChecks()
            .AddCheck(
                HealthDefaults.SelfCheckName,
                () => FrameworkHealthCheckResult.Healthy(),
                [HealthDefaults.LivenessTag]);

        return builder;
    }

    /// <summary>
    /// Maps the default readiness and liveness endpoints.
    /// </summary>
    /// <param name="app">The web application to configure.</param>
    /// <returns>The supplied web application.</returns>
    /// <remarks>
    /// <c>/health</c> evaluates all registered checks and returns the shared
    /// health-report JSON contract. <c>/alive</c> evaluates only checks tagged
    /// with <c>live</c>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="app"/> is <see langword="null"/>.
    /// </exception>
    public static WebApplication MapDefaultEndpoints(
        this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthChecks(
            HealthDefaults.ReadinessEndpointPath,
            new FrameworkHealthCheckOptions {
                ResponseWriter = WriteHealthReportAsync,
            });
        app.MapHealthChecks(
            HealthDefaults.LivenessEndpointPath,
            new FrameworkHealthCheckOptions {
                Predicate = registration =>
                    registration.Tags.Contains(HealthDefaults.LivenessTag),
            });

        return app;
    }

    /// <summary>
    /// Writes a framework health report using the shared health-report JSON
    /// contract.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="report">The framework health report to serialize.</param>
    /// <returns>A task that represents writing the response body.</returns>
    /// <remarks>
    /// The request-aborted token cancels response serialization when the client
    /// disconnects.
    /// </remarks>
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

    /// <summary>
    /// Maps a framework health status to the shared health status contract.
    /// </summary>
    /// <param name="status">The framework health status to map.</param>
    /// <returns>
    /// The corresponding shared status, or <see cref="HealthStatus.Unknown"/>
    /// for an unrecognized value.
    /// </returns>
    private static HealthStatus MapHealthStatus(
        FrameworkHealthStatus status) {
        return status switch {
            FrameworkHealthStatus.Healthy => HealthStatus.Healthy,
            FrameworkHealthStatus.Degraded => HealthStatus.Degraded,
            FrameworkHealthStatus.Unhealthy => HealthStatus.Unhealthy,
            _ => HealthStatus.Unknown,
        };
    }

    /// <summary>
    /// Converts a duration to whole milliseconds for the health-report
    /// contract.
    /// </summary>
    /// <param name="duration">The duration to convert.</param>
    /// <returns>
    /// The duration in milliseconds, rounded upward to avoid reporting a
    /// positive sub-millisecond duration as zero.
    /// </returns>
    private static long ToMilliseconds(TimeSpan duration) {
        return (long)Math.Ceiling(duration.TotalMilliseconds);
    }

    /// <summary>
    /// Determines whether a flags-enum value contains only supported bits.
    /// </summary>
    /// <typeparam name="TEnum">The flags-enum type.</typeparam>
    /// <param name="value">The configured flags value.</param>
    /// <param name="all">The bit mask containing every supported flag.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="value"/> contains no bits
    /// outside <paramref name="all"/>; otherwise <see langword="false"/>.
    /// </returns>
    private static bool HasOnlyDefinedFlags<TEnum>(
        TEnum value,
        TEnum all)
        where TEnum : struct, Enum {
        ulong valueBits = Convert.ToUInt64(value);
        ulong allBits = Convert.ToUInt64(all);
        return (valueBits & ~allBits) == 0;
    }

    /// <summary>
    /// Enables OTLP export when an OTLP exporter endpoint is configured.
    /// </summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The host application builder to configure.</param>
    /// <returns>The supplied host application builder.</returns>
    /// <remarks>
    /// No exporter is added when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is missing,
    /// empty, or whitespace.
    /// </remarks>
    private static TBuilder AddOpenTelemetryExporters<TBuilder>(
        this TBuilder builder)
        where TBuilder : IHostApplicationBuilder {
        if (!string.IsNullOrWhiteSpace(
            builder.Configuration[ConfigurationKeys.OtlpExporterEndpoint])) {
            builder.Services
                .AddOpenTelemetry()
                .UseOtlpExporter();
        }

        return builder;
    }

    /// <summary>
    /// Determines whether ASP.NET Core instrumentation should trace a server
    /// request.
    /// </summary>
    /// <param name="context">The request HTTP context.</param>
    /// <param name="traceMode">The configured trace collection mode.</param>
    /// <returns>
    /// <see langword="true"/> for non-health requests in full mode, or for
    /// scenario requests in scenario-only mode; otherwise
    /// <see langword="false"/>.
    /// </returns>
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
                ScenarioDefaults.EndpointPath, StringComparison.OrdinalIgnoreCase)
            || context.Request.Headers.ContainsKey(
                ScenarioInstrumentation.Headers.ScenarioRun);
    }

    /// <summary>
    /// Determines whether HTTP client instrumentation should trace an outbound
    /// request.
    /// </summary>
    /// <param name="request">The outbound HTTP request.</param>
    /// <param name="traceMode">The configured trace collection mode.</param>
    /// <returns>
    /// <see langword="true"/> in full mode or when the request carries the
    /// scenario header; otherwise <see langword="false"/>.
    /// </returns>
    private static bool ShouldTraceClientRequest(
        HttpRequestMessage request,
        TraceCollectionMode traceMode) {
        return traceMode == TraceCollectionMode.Full
            || request.Headers.Contains(
                ScenarioInstrumentation.Headers.ScenarioRun);
    }

    /// <summary>
    /// Determines whether a request path targets a shared health endpoint.
    /// </summary>
    /// <param name="requestPath">The request path to evaluate.</param>
    /// <returns>
    /// <see langword="true"/> when the path starts with the readiness or
    /// liveness endpoint path; otherwise <see langword="false"/>.
    /// </returns>
    private static bool IsHealthRequest(PathString requestPath) {
        return requestPath.StartsWithSegments(HealthDefaults.ReadinessEndpointPath, StringComparison.OrdinalIgnoreCase)
            || requestPath.StartsWithSegments(HealthDefaults.LivenessEndpointPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Contains the shared health-check names, tags, and endpoint paths.
    /// </summary>
    private static class HealthDefaults {
        /// <summary>
        /// Identifies the readiness health endpoint.
        /// </summary>
        public const string ReadinessEndpointPath = "/health";

        /// <summary>
        /// Identifies the liveness health endpoint.
        /// </summary>
        public const string LivenessEndpointPath = "/alive";

        /// <summary>
        /// Identifies the default process health check.
        /// </summary>
        public const string SelfCheckName = "self";

        /// <summary>
        /// Identifies health checks evaluated by the liveness endpoint.
        /// </summary>
        public const string LivenessTag = "live";
    }

    /// <summary>
    /// Contains stable names used to select framework instrumentation.
    /// </summary>
    private static class InstrumentationNames {
        /// <summary>
        /// Identifies the meter that emits Entity Framework Core metrics.
        /// </summary>
        public const string EntityFrameworkCoreMeter = "Microsoft.EntityFrameworkCore";

        /// <summary>
        /// Identifies the meter that emits Microsoft Orleans metrics.
        /// </summary>
        public const string OrleansMeter = "Microsoft.Orleans";

        /// <summary>
        /// Identifies Microsoft Orleans activity sources by wildcard pattern.
        /// </summary>
        public const string OrleansActivitySource = "Microsoft.Orleans.*";
    }

    /// <summary>
    /// Contains configuration keys consumed by the shared hosting defaults.
    /// </summary>
    private static class ConfigurationKeys {
        /// <summary>
        /// Identifies the setting that enables OTLP export.
        /// </summary>
        public const string OtlpExporterEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
    }

    /// <summary>
    /// Contains routing values used to identify scenario trace traffic.
    /// </summary>
    private static class ScenarioDefaults {
        /// <summary>
        /// Identifies the scenario execution endpoint used for scenario-only
        /// trace collection.
        /// </summary>
        public const string EndpointPath = "/api/scenarios/run";
    }
}
