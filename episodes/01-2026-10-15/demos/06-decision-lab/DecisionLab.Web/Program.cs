/*
 * Goal: Host an interactive, server-side decision comparison.
 * A. Register Aspire telemetry without recording prompt bodies.
 * B. Register the two model clients and circuit-scoped run state.
 * C. Serve the Blazor page; model credentials never go to the browser.
 */
using DecisionLab.Web;
using DecisionLab.Web.Components;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSingleton<IModelComparison, ModelComparison>();
builder.Services.AddScoped<ComparisonRun>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, RunCircuitHandler>();
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();
app.Run();
