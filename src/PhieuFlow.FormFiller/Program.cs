using Serilog;
using PhieuFlow.FormFiller.Clients;
using PhieuFlow.FormFiller.Components;
using PhieuFlow.FormFiller.Submissions;
using PhieuFlow.Hub.Contracts.Submissions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Submission transport (ADR 0001): the Aspire "rabbitmq" resource supplies the connection
// string; RabbitMqSubmissionPublisher declares the durable queue and publishes to it.
builder.AddRabbitMQClient(connectionName: "rabbitmq");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Service-to-service auth (ADR 0005): obtain an OAuth2 client-credentials token from
// Keycloak, scoped to published-forms:read only, and attach it to every Hub call.
builder.AddKeycloakClientCredentials(defaultScope: "published-forms:read");

builder.Services.AddHttpClient<IHubFormsClient, HubFormsClient>(client =>
{
    client.BaseAddress = new Uri("https+http://hub");
})
.AddClientCredentialsToken();

builder.Services.AddScoped<ISubmissionPublisher, RabbitMqSubmissionPublisher>();

// Client-side answer validation: the submission is published fire-and-forget (ADR 0009),
// so FillPage must gate it against each question's constraints before it leaves. Same
// validator the Hub consumer re-runs on the way in.
builder.Services.AddSingleton<SubmissionAnswersValidator>();

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
