using Comparison.Ui.Components;
using Comparison.Ui.Services;
using Hosting.ServiceDefaults.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add shared Aspire service discovery, resilience, health checks, and OpenTelemetry.
builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<ScenarioRunnerClient>(client => {
    var baseUrl = builder.Configuration[$"Gateway:BaseUrl"] ?? $"http://localhost:5100";
    client.BaseAddress = new Uri(baseUrl);
});

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler($"/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map the shared health and aliveness endpoints.
app.MapDefaultEndpoints();

await app.RunAsync().ConfigureAwait(false);
