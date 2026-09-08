using Serilog;
using PhieuFlow.FormBuilder.Clients;
using PhieuFlow.FormBuilder.Components;
using PhieuFlow.FormBuilder.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Service-to-service auth (ADR 0005): obtain an OAuth2 client-credentials token from
// Keycloak and attach it to every Hub call.
builder.AddKeycloakClientCredentials(defaultScope: "forms:write");

builder.Services.AddHttpClient<IHubFormsClient, HubFormsClient>(client =>
{
    client.BaseAddress = new Uri("https+http://hub");
})
.AddClientCredentialsToken();

builder.Services.AddScoped<IFormsService, FormsService>();

var app = builder.Build();

app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
