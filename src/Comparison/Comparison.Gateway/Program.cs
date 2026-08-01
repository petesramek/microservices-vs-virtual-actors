using ArchitectureComparison.Contracts;
using Comparison.Gateway.Clients;
using Comparison.Gateway.Configuration;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddProblemDetails();

builder.Services
    .AddOptions<ArchitectureEndpointOptions>()
    .Bind(builder.Configuration.GetSection(ArchitectureEndpointOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var endpointOptions = builder.Configuration
    .GetSection(ArchitectureEndpointOptions.SectionName)
    .Get<ArchitectureEndpointOptions>() ?? new ArchitectureEndpointOptions();

builder.Services.AddHttpClient<MicroservicesArchitectureClient>(client => {
    client.BaseAddress = new Uri(endpointOptions.MicroservicesBaseUrl);
});

builder.Services.AddHttpClient<VirtualActorsArchitectureClient>(client => {
    client.BaseAddress = new Uri(endpointOptions.VirtualActorsBaseUrl);
});

var app = builder.Build();
// correlation-id-logging
app.Use(async (context, next) => {
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId)) {
        correlationId = $"run-{Guid.NewGuid():N}";
    }

    context.Response.Headers["X-Correlation-ID"] = correlationId;
    CorrelationContext.CurrentCorrelationId = correlationId;

    using var scope = app.Logger.BeginScope(new Dictionary<string, object> {
        ["CorrelationId"] = correlationId
    });

    app.Logger.LogInformation("Handling request with correlation id {CorrelationId}.", correlationId);

    try {
        await next();
    } finally {
        CorrelationContext.CurrentCorrelationId = null;
    }
});

app.UseExceptionHandler();

app.MapGet("/", () => Results.Ok(new {
    Name = "Architecture Comparison Gateway",
    Description = "Routes scenario requests by X-Architecture header."
}));

app.MapGet("/api/status", async (
    IHttpClientFactory httpClientFactory,
    IOptions<ArchitectureEndpointOptions> options,
    CancellationToken cancellationToken) => {
        var httpClient = httpClientFactory.CreateClient();
        var gateway = new ServiceStatus("Gateway", "local", true, "Online", null);
        var microservices = await CheckBackendAsync(
            httpClient,
            "Microservices",
            options.Value.MicroservicesBaseUrl,
            cancellationToken);
        var virtualActors = await CheckBackendAsync(
            httpClient,
            "Virtual Actors",
            options.Value.VirtualActorsBaseUrl,
            cancellationToken);

        return Results.Ok(new BackendStatusResponse(gateway, microservices, virtualActors));
    });

app.MapPost("/api/scenarios/run", RunScenario);

app.Run();

static async Task<ServiceStatus> CheckBackendAsync(
    HttpClient httpClient,
    string name,
    string baseUrl,
    CancellationToken cancellationToken) {
    var healthUrl = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), "health/live");

    try {
        using var response = await httpClient.GetAsync(healthUrl, cancellationToken);
        return new ServiceStatus(
            name,
            healthUrl.ToString(),
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode} {response.StatusCode}",
            response.IsSuccessStatusCode ? null : "Health endpoint returned a non-success status code.");
    } catch (Exception exception) {
        return new ServiceStatus(
            name,
            healthUrl.ToString(),
            false,
            "Unavailable",
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
    var architecture = httpRequest.Headers.TryGetValue("X-Architecture", out var values)
        ? values.FirstOrDefault() ?? "both"
        : "both";

    var logger = loggerFactory.CreateLogger("Comparison.Gateway");
    logger.LogInformation("Running scenario {Scenario} for architecture selection {Architecture}", request.Scenario, architecture);

    ArchitectureRunResult? microservices = null;
    ArchitectureRunResult? virtualActors = null;

    if (architecture.Equals("microservices", StringComparison.OrdinalIgnoreCase) || architecture.Equals("both", StringComparison.OrdinalIgnoreCase)) {
        microservices = await microservicesClient.RunAsync(request, cancellationToken);
    }

    if (architecture.Equals("virtual-actors", StringComparison.OrdinalIgnoreCase) || architecture.Equals("both", StringComparison.OrdinalIgnoreCase)) {
        virtualActors = await virtualActorsClient.RunAsync(request, cancellationToken);
    }

    if (microservices is null && virtualActors is null) {
        return Results.BadRequest(new { Error = "Unsupported X-Architecture value. Use microservices, virtual-actors, or both." });
    }

    return Results.Ok(new RunScenarioResponse(request.Scenario, microservices, virtualActors));
}

public partial class Program;

