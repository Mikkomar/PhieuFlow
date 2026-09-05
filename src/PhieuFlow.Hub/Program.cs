using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using PhieuFlow.Hub.Authorization;
using PhieuFlow.Hub.Endpoints;
using PhieuFlow.Hub.Validation;
using PhieuFlow.Persistence;
using PhieuFlow.Persistence.Repositories;
using PhieuFlow.Persistence.UnitOfWork;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<HubDbContext>("HubDatabase");

builder.Services.AddScoped<IFormRepository, FormRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IFormPublishValidator, FormPublishValidator>();

// Service-to-service auth (ADR 0005): validate OAuth2 client-credentials tokens with
// standard JWT bearer middleware against the IdP's OIDC metadata. Everything Keycloak-
// specific lives in configuration (Keycloak:Authority) — swapping to Entra ID is a
// config change, not a code change.
var authority = builder.Configuration["Keycloak:Authority"]
    ?? throw new InvalidOperationException("Keycloak:Authority is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = builder.Configuration["Keycloak:Audience"] ?? "phieuflow-hub";
        options.RequireHttpsMetadata =
            builder.Configuration.GetValue("Keycloak:RequireHttpsMetadata", true);
        // Keep raw claim names (scope, azp, sub) rather than the legacy SOAP URIs.
        options.MapInboundClaims = false;
        // ValidIssuer defaults to Authority; the token iss and the metadata issuer are
        // the same because every caller reaches Keycloak through that one URL. Do not
        // constrain token 'typ' — some Keycloak builds stamp "Bearer" not "at+jwt".

        // Local orchestration only: Aspire serves Keycloak's OIDC metadata over a
        // self-signed certificate. A real deployment leaves this unset and uses a
        // trusted authority.
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
});
builder.Services.AddSingleton<IAuthorizationHandler, ScopeHandler>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapFormEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in PhieuFlow.Tests.Integration.
public partial class Program;
