var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");
var hubDb = sql.AddDatabase("HubDatabase", "PhieuFlowHub");

// Keycloak is the local identity provider for service-to-service auth (ADR 0005).
const string hubAudience = "phieuflow-hub";
const string formBuilderClientId = "form-builder";
const string formBuilderClientSecret = "form-builder-dev-secret";
const string formFillerClientId = "form-filler";
const string formFillerClientSecret = "form-filler-dev-secret";

var keycloak = builder.AddKeycloak("keycloak")
    .WithRealmImport(Path.Combine(builder.AppHostDirectory, "realms"));

// The one deliberate async boundary (ADR 0001): the form-filler publishes completed
// responses onto the durable `form-submissions` queue and the Hub consumer drains it.
// The data volume keeps persistent messages across a broker restart; the management
// plugin exposes the queue/DLX state for inspection during development.
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithDataVolume()
    .WithManagementPlugin();

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
    .WithReference(rabbitmq)
    .WaitFor(keycloak)
    .WaitFor(rabbitmq)
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

// Both halves of the ADR 0001 async boundary are wired: the form-filler publishes to
// `form-submissions` and the Hub's SubmissionConsumerService drains it (ADR 0009).
builder.AddProject<Projects.PhieuFlow_FormFiller>("formfiller")
    .WithExternalHttpEndpoints()
    .WithReference(hub)
    .WithReference(keycloak)
    .WithReference(rabbitmq)
    .WaitFor(hub)
    .WaitFor(keycloak)
    .WaitFor(rabbitmq)
    .WithEnvironment("Keycloak__Authority", keycloakRealmAuthority)
    .WithEnvironment("Keycloak__ClientId", formFillerClientId)
    .WithEnvironment("Keycloak__ClientSecret", formFillerClientSecret)
    .WithEnvironment("Keycloak__RequireHttpsMetadata", "false")
    .WithEnvironment("Keycloak__DangerousAcceptAnyServerCertificate", "true")
    .WithHttpHealthCheck("/health");

builder.Build().Run();
