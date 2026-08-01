using Comparison.Contracts;
using Comparison.Gateway.Clients;
using Comparison.Gateway.Configuration;
using Microsoft.Extensions.Options;
using Comparison.Gateway.Logging;
using Microsoft.Extensions.Primitives;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddProblemDetails();

builder.Services
    .AddOptions<ArchitectureEndpointOptions>()
    .Bind(builder.Configuration.GetSection(ArchitectureEndpointOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

ArchitectureEndpointOptions endpointOptions = builder.Configuration
    .GetSection(ArchitectureEndpointOptions.SectionName)
    .Get<ArchitectureEndpointOptions>() ?? new ArchitectureEndpointOptions();

builder.Services.AddHttpClient<MicroservicesArchitectureClient>(client => {
    client.BaseAddress = new Uri(endpointOptions.MicroservicesBaseUrl);
});

builder.Services.AddHttpClient<VirtualActorsArchitectureClient>(client => {
    client.BaseAddress = new Uri(endpointOptions.VirtualActorsBaseUrl);
});

WebApplication app = builder.Build();
// correlation-id-logging
app.Use(async (context, next) => {
    var correlationId = context.Request.Headers[$"X-Correlation-ID"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId)) {
        correlationId = $"run-{Guid.NewGuid():N}";
    }

    context.Response.Headers[$"X-Correlation-ID"] = correlationId;
    CorrelationContext.CurrentCorrelationId = correlationId;

    using IDisposable? scope = app.Logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal) {
        [$"CorrelationId"] = correlationId,
    });

    app.Logger.HandlingRequestWithCorrelationId(correlationId);

    try {
        await next().ConfigureAwait(false);
    } finally {
        CorrelationContext.CurrentCorrelationId = null;
    }
});

app.UseExceptionHandler();

app.MapGet($"/", () => Results.Ok(new {
    Name = $"Architecture Comparison Gateway",
    Description = $"Routes scenario requests by X-Architecture header.",
}));

app.MapGet($"/api/status", async (
    IHttpClientFactory httpClientFactory,
    IOptions<ArchitectureEndpointOptions> options,
    CancellationToken cancellationToken) => {
        HttpClient httpClient = httpClientFactory.CreateClient();
        var gateway = new ServiceStatus($"Gateway", $"local", IsOnline: true, $"Online", Error: null);
        ServiceStatus microservices = await CheckBackendAsync(
            httpClient,
            $"Microservices",
            options.Value.MicroservicesBaseUrl,
            cancellationToken).ConfigureAwait(false);
        ServiceStatus virtualActors = await CheckBackendAsync(
            httpClient,
            $"Virtual Actors",
            options.Value.VirtualActorsBaseUrl,
            cancellationToken).ConfigureAwait(false);

        return Results.Ok(new BackendStatusResponse(gateway, microservices, virtualActors));
    });

app.MapPost($"/api/scenarios/run", RunScenario);
await app.RunAsync().ConfigureAwait(false);

static async Task<ServiceStatus> CheckBackendAsync(
    HttpClient httpClient,
    string name,
    string baseUrl,
    CancellationToken cancellationToken) {
    var healthUrl = new Uri(new Uri(baseUrl.TrimEnd('/') + $"/"), $"health/live");

    try {
        using HttpResponseMessage response = await httpClient.GetAsync(healthUrl, cancellationToken).ConfigureAwait(false);
        return new ServiceStatus(
            name,
            healthUrl.ToString(),
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode} {response.StatusCode}",
            response.IsSuccessStatusCode ? null : $"Health endpoint returned a non-success status code.");
    } catch (Exception exception) {
        return new ServiceStatus(
            name,
            healthUrl.ToString(),
IsOnline: false,
$"Unavailable",
            exception.Message);
    }
}

static async Task<IResult> RunScenario(
    RunScenarioRequest request,
    HttpRequest httpRequest,
    MicroservicesArchitectureClient microservicesClient,
    VirtualActorsArchitectureClient virtualActorsClient,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) {
    var architecture = httpRequest.Headers.TryGetValue($"X-Architecture", out StringValues values)
        ? values.FirstOrDefault() ?? $"both" : $"both";

    ILogger logger = loggerFactory.CreateLogger($"Comparison.Gateway");

    logger.RunningScenarioForArchitecture(request.Scenario, architecture);

    ArchitectureRunResult? microservices = null;
    ArchitectureRunResult? virtualActors = null;

    if (architecture.Equals($"microservices", StringComparison.OrdinalIgnoreCase) || architecture.Equals($"both", StringComparison.OrdinalIgnoreCase)) {
        microservices = await microservicesClient.RunAsync(request, cancellationToken).ConfigureAwait(false);
    }

    if (architecture.Equals($"virtual-actors", StringComparison.OrdinalIgnoreCase) || architecture.Equals($"both", StringComparison.OrdinalIgnoreCase)) {
        virtualActors = await virtualActorsClient.RunAsync(request, cancellationToken).ConfigureAwait(false);
    }

    if (microservices is null && virtualActors is null) {
        return Results.BadRequest(new { Error = $"Unsupported X-Architecture value. Use microservices, virtual-actors, or both." });
    }

    return Results.Ok(new RunScenarioResponse(request.Scenario, microservices, virtualActors));
}

public partial class Program;

