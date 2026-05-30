using ArchitectureComparison.Contracts;
using Comparison.Gateway.Clients;
using Comparison.Gateway.Configuration;

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

builder.Services.AddHttpClient<MicroservicesArchitectureClient>(client =>
{
    client.BaseAddress = new Uri(endpointOptions.MicroservicesBaseUrl);
});

builder.Services.AddHttpClient<VirtualActorsArchitectureClient>(client =>
{
    client.BaseAddress = new Uri(endpointOptions.VirtualActorsBaseUrl);
});

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/", () => Results.Ok(new
{
    Name = "Architecture Comparison Gateway",
    Description = "Routes scenario requests by X-Architecture header."
}));

app.MapPost("/api/scenarios/run", RunScenario);

app.Run();

static async Task<IResult> RunScenario(
    RunScenarioRequest request,
    HttpRequest httpRequest,
    MicroservicesArchitectureClient microservicesClient,
    VirtualActorsArchitectureClient virtualActorsClient,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken)
{
    var architecture = httpRequest.Headers.TryGetValue("X-Architecture", out var values)
        ? values.FirstOrDefault() ?? "both"
        : "both";

    var logger = loggerFactory.CreateLogger("Comparison.Gateway");
    logger.LogInformation("Running scenario {Scenario} for architecture selection {Architecture}", request.Scenario, architecture);

    ArchitectureRunResult? microservices = null;
    ArchitectureRunResult? virtualActors = null;

    if (architecture.Equals("microservices", StringComparison.OrdinalIgnoreCase) || architecture.Equals("both", StringComparison.OrdinalIgnoreCase))
    {
        microservices = await microservicesClient.RunAsync(request, cancellationToken);
    }

    if (architecture.Equals("virtual-actors", StringComparison.OrdinalIgnoreCase) || architecture.Equals("both", StringComparison.OrdinalIgnoreCase))
    {
        virtualActors = await virtualActorsClient.RunAsync(request, cancellationToken);
    }

    if (microservices is null && virtualActors is null)
    {
        return Results.BadRequest(new { Error = "Unsupported X-Architecture value. Use microservices, virtual-actors, or both." });
    }

    return Results.Ok(new RunScenarioResponse(request.Scenario, microservices, virtualActors));
}

public partial class Program;
