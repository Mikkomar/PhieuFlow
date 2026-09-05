var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");
var hubDb = sql.AddDatabase("HubDatabase", "PhieuFlowHub");

// Keycloak is the local identity provider for service-to-service auth (ADR 0005).
const string hubAudience = "phieuflow-hub";
const string formBuilderClientId = "form-builder";
const string formBuilderClientSecret = "form-builder-dev-secret";

var keycloak = builder.AddKeycloak("keycloak")
    .WithRealmImport(Path.Combine(builder.AppHostDirectory, "realms"));

// The hub, the form-builder and the E2E tests all reach Keycloak through this one
// endpoint, so the token issuer, the OIDC metadata address and the issuer the hub
// validates against are guaranteed to be the same string — whatever host/port Aspire
// assigns. In a real deployment Keycloak:Authority is set to a fixed external URL
// (or swapped to Entra ID) and this wiring is not used.
var keycloakRealmAuthority = ReferenceExpression.Create(
    $"{keycloak.GetEndpoint("http")}/realms/phieuflow");

var migrations = builder.AddProject<Projects.PhieuFlow_MigrationService>("migrations")
    .WithReference(hubDb)
    .WaitFor(hubDb);

var hubBuilder = builder.AddProject<Projects.PhieuFlow_Hub>("hub")
    .WithReference(hubDb)
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithEnvironment("Keycloak__Authority", keycloakRealmAuthority)
    .WithEnvironment("Keycloak__Audience", hubAudience)
    .WithEnvironment("Keycloak__RequireHttpsMetadata", "false")
    .WithEnvironment("Keycloak__DangerousAcceptAnyServerCertificate", "true")
    .WaitForCompletion(migrations);

// Sample data is for local development only; production data comes from real usage.
if (!builder.ExecutionContext.IsPublishMode)
{
    var seed = builder.AddProject<Projects.PhieuFlow_SeedService>("seed")
        .WithReference(hubDb)
        .WaitForCompletion(migrations);

    hubBuilder = hubBuilder.WaitForCompletion(seed);
}

var hub = hubBuilder.WithHttpHealthCheck("/health");

builder.AddProject<Projects.PhieuFlow_FormBuilder>("formbuilder")
    .WithExternalHttpEndpoints()
    .WithReference(hub)
    .WithReference(keycloak)
    .WaitFor(hub)
    .WaitFor(keycloak)
    .WithEnvironment("Keycloak__Authority", keycloakRealmAuthority)
    .WithEnvironment("Keycloak__ClientId", formBuilderClientId)
    .WithEnvironment("Keycloak__ClientSecret", formBuilderClientSecret)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.PhieuFlow_FormFiller>("formfiller")
    .WithExternalHttpEndpoints()
    .WithReference(hub)
    .WaitFor(hub)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
