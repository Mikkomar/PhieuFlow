using Serilog;
using PhieuFlow.FormBuilder.Clients;
using PhieuFlow.FormBuilder.Components;
using PhieuFlow.FormBuilder.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Get an OAuth2 client-credentials token from Keycloak and attach it to every Hub call.
builder.AddKeycloakClientCredentials(defaultScope: "forms:write");

builder.Services.AddHttpClient<IHubFormsClient, HubFormsClient>(client =>
{
    client.BaseAddress = new Uri("https+http://hub");
})
.AddClientCredentialsToken();

builder.Services.AddScoped<IFormsService, FormsService>();

var app = builder.Build();

app.UseSerilogRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS lifetime is 30 days. Change it for production. See https://aka.ms/aspnetcore-hsts.
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
