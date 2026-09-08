using Serilog;
using PhieuFlow.FormFiller.Clients;
using PhieuFlow.FormFiller.Components;
using PhieuFlow.FormFiller.Submissions;
using PhieuFlow.Hub.Contracts.Submissions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// RabbitMqSubmissionPublisher declares the durable submission queue and publishes to it.
builder.AddRabbitMQClient(connectionName: "rabbitmq");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Get an OAuth2 client-credentials token from Keycloak, scoped to published-forms:read,
// and attach it to every Hub call.
builder.AddKeycloakClientCredentials(defaultScope: "published-forms:read");

builder.Services.AddHttpClient<IHubFormsClient, HubFormsClient>(client =>
{
    client.BaseAddress = new Uri("https+http://hub");
})
.AddClientCredentialsToken();

builder.Services.AddScoped<ISubmissionPublisher, RabbitMqSubmissionPublisher>();

// The submission is published without waiting for a result, so FillPage must check it
// against each question's constraints first. The Hub consumer runs the same validator.
builder.Services.AddSingleton<SubmissionAnswersValidator>();

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
