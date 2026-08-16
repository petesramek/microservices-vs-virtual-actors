using Hosting.ServiceDefaults.Extensions;
using Workbench.Ui.Components;
using Workbench.Ui.Internal.Extensions;
using Workbench.Ui.Internal.Services;
using Workbench.Ui.Observability.Topology;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add shared Aspire service discovery, resilience, health checks, and OpenTelemetry.
builder.AddServiceDefaults();

string gatewayBaseUrl =
    builder.Configuration["Gateway:BaseUrl"]
    ?? "http://localhost:5100";

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<ScenarioRunnerClient>(client => {
    client.BaseAddress = new Uri(gatewayBaseUrl);
});

builder.Services
    .AddHealthChecks();

// Configure and validate the observable topology definition.
builder.Services
    .AddOptions<TopologyOptions>()
    .Bind(builder.Configuration.GetSection(TopologyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<TopologyDefinitionProvider>();

// Configure hierarchical system health collection.
builder.Services.AddSystemHealth(builder.Configuration);

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map the shared health and aliveness endpoints.
app.MapDefaultEndpoints();

await app.RunAsync().ConfigureAwait(false);
