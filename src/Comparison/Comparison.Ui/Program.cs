using Comparison.Ui.Components;
using Comparison.Ui.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

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
await app.RunAsync().ConfigureAwait(false);
