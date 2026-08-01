using Comparison.Contracts;
using Comparison.Gateway.Clients;
using Comparison.Gateway.Configuration;
using Comparison.Gateway.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Keep console logging as the single provider so local runs and a future Aspire dashboard receive the same events.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Produce standardized problem-details responses for unhandled gateway failures.
builder.Services.AddProblemDetails();

// Bind and validate the downstream architecture endpoints during application startup.
builder.Services
    .AddOptions<ArchitectureEndpointOptions>()
    .Bind(builder.Configuration.GetSection(ArchitectureEndpointOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Configure the client that runs scenarios through the Microservices architecture.
builder.Services.AddHttpClient<MicroservicesArchitectureClient>((services, client) => {
    ArchitectureEndpointOptions options = services
        .GetRequiredService<IOptions<ArchitectureEndpointOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.MicroservicesBaseUrl);
});

// Configure the client that runs scenarios through the Virtual Actors architecture.
builder.Services.AddHttpClient<VirtualActorsArchitectureClient>((services, client) => {
    ArchitectureEndpointOptions options = services
        .GetRequiredService<IOptions<ArchitectureEndpointOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.VirtualActorsBaseUrl);
});

WebApplication app = builder.Build();

// Establish one correlation identifier for the gateway request and all downstream calls.
app.Use(async (context, next) => {
    const string CorrelationIdHeader = "X-Correlation-ID";

    // Preserve a caller-supplied identifier or create one at the gateway boundary.
    var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId)) {
        correlationId = $"run-{Guid.NewGuid():N}";
    }

    // Return the identifier to the caller and expose it to the downstream architecture clients.
    context.Response.Headers[CorrelationIdHeader] = correlationId;
    CorrelationContext.CurrentCorrelationId = correlationId;

    // Enrich every gateway log written during this request with the same identifier.
    using IDisposable? scope = app.Logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal) {
        ["CorrelationId"] = correlationId,
    });

    app.Logger.HandlingRequestWithCorrelationId(correlationId);

    try {
        await next().ConfigureAwait(false);
    } finally {
        // Prevent the ambient correlation identifier from leaking into a later request.
        CorrelationContext.CurrentCorrelationId = null;
    }
});

// Convert unhandled failures outside the explicitly handled scenario flow into problem-details responses.
app.UseExceptionHandler();

// Describe the gateway and its architecture-selection contract.
app.MapGet("/", () => Results.Ok(new {
    Name = "Architecture Comparison Gateway",
    Description = "Routes scenario requests by X-Architecture header.",
}));

// Report the current reachability of the gateway and both architecture backends.
app.MapGet("/api/status", async (
    IHttpClientFactory httpClientFactory,
    IOptions<ArchitectureEndpointOptions> options,
    CancellationToken cancellationToken) => {
    HttpClient httpClient = httpClientFactory.CreateClient();
    var gateway = new ServiceStatus("Gateway", "local", IsOnline: true, "Online", Error: null);

    // Start both independent health checks before awaiting either result.
    Task<ServiceStatus> microservicesTask = CheckBackendAsync(
        httpClient,
        "Microservices",
        options.Value.MicroservicesBaseUrl,
        cancellationToken);

    Task<ServiceStatus> virtualActorsTask = CheckBackendAsync(
        httpClient,
        "Virtual Actors",
        options.Value.VirtualActorsBaseUrl,
        cancellationToken);

    ServiceStatus[] backendStatuses = await Task.WhenAll(
        microservicesTask,
        virtualActorsTask).ConfigureAwait(false);

    return Results.Ok(new BackendStatusResponse(
        gateway,
        backendStatuses[0],
        backendStatuses[1]));
});

// Run the selected comparison scenario against one or both architectures.
app.MapPost("/api/scenarios/run", RunScenario);

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Checks whether an architecture backend responds successfully to its health endpoint.
/// </summary>
/// <param name="httpClient">The HTTP client used to call the backend.</param>
/// <param name="name">The display name of the backend.</param>
/// <param name="baseUrl">The backend base URL.</param>
/// <param name="cancellationToken">The token used to cancel the request.</param>
/// <returns>The current backend service status.</returns>
static async Task<ServiceStatus> CheckBackendAsync(
    HttpClient httpClient,
    string name,
    string baseUrl,
    CancellationToken cancellationToken) {
    var healthUrl = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), "health/live");

    // Health-check failures are returned as backend status rather than failing the gateway status endpoint.
    try {
        using HttpResponseMessage response = await httpClient.GetAsync(healthUrl, cancellationToken).ConfigureAwait(false);

        return new ServiceStatus(
            name,
            healthUrl.ToString(),
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode} {response.StatusCode}",
            response.IsSuccessStatusCode
                ? null
                : "Health endpoint returned a non-success status code.");
    }
    catch (OperationCanceledException) {
        // Preserve request cancellation instead of reporting the backend as unavailable.
        throw;
    }
    catch (Exception exception) {
        return new ServiceStatus(
            name,
            healthUrl.ToString(),
            IsOnline: false,
            "Unavailable",
            exception.Message);
    }
}

/// <summary>
/// Runs a comparison scenario against the architecture selected by the request header.
/// </summary>
/// <param name="request">The scenario request.</param>
/// <param name="httpRequest">The current HTTP request containing the architecture selection.</param>
/// <param name="microservicesClient">The Microservices architecture client.</param>
/// <param name="virtualActorsClient">The Virtual Actors architecture client.</param>
/// <param name="loggerFactory">The logger factory.</param>
/// <param name="cancellationToken">The token used to cancel scenario execution.</param>
/// <returns>The scenario result or an error response.</returns>
static async Task<IResult> RunScenario(
    RunScenarioRequest request,
    HttpRequest httpRequest,
    MicroservicesArchitectureClient microservicesClient,
    VirtualActorsArchitectureClient virtualActorsClient,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) {
    const string ArchitectureHeader = "X-Architecture";
    const string BothArchitectures = "both";
    const string MicroservicesArchitecture = "microservices";
    const string VirtualActorsArchitecture = "virtual-actors";

    // Default to both architectures when the caller does not provide an explicit selection.
    var architecture = httpRequest.Headers.TryGetValue(ArchitectureHeader, out StringValues values)
        ? values.FirstOrDefault() ?? BothArchitectures
        : BothArchitectures;

    // Resolve the selection once so the execution branches remain simple and explicit.
    bool runMicroservices = architecture.Equals(MicroservicesArchitecture, StringComparison.OrdinalIgnoreCase)
        || architecture.Equals(BothArchitectures, StringComparison.OrdinalIgnoreCase);
    bool runVirtualActors = architecture.Equals(VirtualActorsArchitecture, StringComparison.OrdinalIgnoreCase)
        || architecture.Equals(BothArchitectures, StringComparison.OrdinalIgnoreCase);

    ILogger logger = loggerFactory.CreateLogger("Comparison.Gateway");

    // Reject unsupported selections before reporting that scenario execution has started.
    if (!runMicroservices && !runVirtualActors) {
        logger.UnsupportedArchitectureRequested(architecture);

        return Results.BadRequest(new {
            Error = "Unsupported X-Architecture value. Use microservices, virtual-actors, or both.",
        });
    }

    logger.RunningScenario(request.Scenario, architecture);

    try {
        ArchitectureRunResult? microservices = null;
        ArchitectureRunResult? virtualActors = null;

        // Start both independent architecture runs together when the caller requests a comparison.
        if (runMicroservices && runVirtualActors) {
            Task<ArchitectureRunResult> microservicesTask = microservicesClient.RunAsync(
                request,
                cancellationToken);
            Task<ArchitectureRunResult> virtualActorsTask = virtualActorsClient.RunAsync(
                request,
                cancellationToken);

            ArchitectureRunResult[] results = await Task.WhenAll(
                microservicesTask,
                virtualActorsTask).ConfigureAwait(false);

            microservices = results[0];
            virtualActors = results[1];
        }
        else if (runMicroservices) {
            microservices = await microservicesClient.RunAsync(request, cancellationToken).ConfigureAwait(false);
        }
        else {
            virtualActors = await virtualActorsClient.RunAsync(request, cancellationToken).ConfigureAwait(false);
        }

        logger.ScenarioCompleted(
            request.Scenario,
            architecture,
            microservices is not null,
            virtualActors is not null);

        return Results.Ok(new RunScenarioResponse(request.Scenario, microservices, virtualActors));
    }
    catch (OperationCanceledException) {
        // Preserve cancellation so the hosting pipeline can handle it correctly.
        throw;
    }
    catch (Exception exception) {
        // Add scenario context once and convert the unexpected failure into a stable API response.
        logger.ScenarioExecutionFailed(exception, request.Scenario, architecture);

        return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
    }
}

public partial class Program;
