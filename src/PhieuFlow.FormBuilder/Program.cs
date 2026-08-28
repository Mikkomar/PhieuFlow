using PhieuFlow.FormBuilder.Components;
using PhieuFlow.FormBuilder.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<FormRepository>();

// Service-to-service auth (ADR 0005): obtain an OAuth2 client-credentials token from
// Keycloak and attach it to every Hub call.
builder.Services.Configure<KeycloakClientOptions>(builder.Configuration.GetSection("Keycloak"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ClientCredentialsTokenProvider>();
builder.Services.AddTransient<ClientCredentialsTokenHandler>();

var keycloakTokenClient = builder.Services.AddHttpClient("keycloak-token");
if (builder.Environment.IsDevelopment())
{
    // Local orchestration only: Aspire serves Keycloak over a self-signed certificate.
    keycloakTokenClient.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
    });
}

builder.Services.AddHttpClient<HubFormsClient>(client =>
{
    client.BaseAddress = new Uri("https+http://hub");
})
.AddHttpMessageHandler<ClientCredentialsTokenHandler>();

builder.Services.AddScoped<IFormsService, FormsService>();

var app = builder.Build();

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
