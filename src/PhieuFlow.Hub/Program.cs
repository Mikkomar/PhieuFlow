using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Serilog;
using PhieuFlow.Hub.Authorization;
using PhieuFlow.Hub.Contracts.Submissions;
using PhieuFlow.Hub.Endpoints;
using PhieuFlow.Hub.Submissions;
using PhieuFlow.Hub.Contracts.Validation;
using PhieuFlow.Persistence;
using PhieuFlow.Persistence.Reconciliation;
using PhieuFlow.Persistence.Repositories;
using PhieuFlow.Persistence.UnitOfWork;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<HubDbContext>("HubDatabase");

builder.Services.AddScoped<IFormRepository, FormRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IFormPublishValidator, FormPublishValidator>();

// On an edit to a published version, fork a new version and reconcile the tree.
// FormRepository drives this. No DbContext here.
builder.Services.AddScoped<IFormTreeCloner, FormTreeCloner>();
builder.Services.AddScoped<IFormVersionReconciler, FormVersionReconciler>();

// SubmissionConsumerService reads the form-submissions queue and persists each response
// through SubmissionMessageHandler.
builder.AddRabbitMQClient(connectionName: "rabbitmq");
builder.Services.Configure<SubmissionConsumerOptions>(
    builder.Configuration.GetSection(SubmissionConsumerOptions.SectionName));
builder.Services.AddSingleton<SubmissionAnswersValidator>();
builder.Services.AddScoped<SubmissionMessageHandler>();
builder.Services.AddHostedService<SubmissionConsumerService>();

// Validate OAuth2 client-credentials tokens with standard JWT bearer middleware. All
// Keycloak-specific values are in configuration, so a move to Entra ID needs no code change.
var authority = builder.Configuration["Keycloak:Authority"]
    ?? throw new InvalidOperationException("Keycloak:Authority is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = builder.Configuration["Keycloak:Audience"] ?? "phieuflow-hub";
        options.RequireHttpsMetadata =
            builder.Configuration.GetValue("Keycloak:RequireHttpsMetadata", true);
        // Keep raw claim names (scope, azp, sub) instead of the legacy SOAP URIs.
        options.MapInboundClaims = false;
        // ValidIssuer defaults to Authority, which every caller uses. Do not constrain the
        // token "typ". Some Keycloak builds stamp "Bearer", not "at+jwt".

        // Local only: Aspire serves the Keycloak metadata over a self-signed certificate.
        // A real deployment leaves this unset.
        if (builder.Configuration.GetValue("Keycloak:DangerousAcceptAnyServerCertificate", false))
        {
            options.BackchannelHttpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            };
        }
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("forms:read", p => p.Requirements.Add(new ScopeRequirement("forms:read")));
    options.AddPolicy("forms:write", p => p.Requirements.Add(new ScopeRequirement("forms:write")));
    options.AddPolicy("published-forms:read", p => p.Requirements.Add(new ScopeRequirement("published-forms:read")));
    options.AddPolicy("submissions:read", p => p.Requirements.Add(new ScopeRequirement("submissions:read")));
});
builder.Services.AddSingleton<IAuthorizationHandler, ScopeHandler>();

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapFormEndpoints();

app.Run();

// Made public for WebApplicationFactory<Program> in PhieuFlow.Tests.Integration.
public partial class Program;
