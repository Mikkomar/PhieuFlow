var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");
var hubDb = sql.AddDatabase("HubDatabase", "PhieuFlowHub");

// Keycloak is the local identity provider for service-to-service auth.
const string hubAudience = "phieuflow-hub";
const string formBuilderClientId = "form-builder";
const string formBuilderClientSecret = "form-builder-dev-secret";
const string formFillerClientId = "form-filler";
const string formFillerClientSecret = "form-filler-dev-secret";

var keycloak = builder.AddKeycloak("keycloak")
    .WithRealmImport(Path.Combine(builder.AppHostDirectory, "realms"));

// The form-filler publishes completed responses onto the durable `form-submissions`
// queue and the Hub consumer reads it. The data volume survives a broker restart.
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithDataVolume()
    .WithManagementPlugin();

// Every service and the E2E tests reach Keycloak through this one endpoint, so the
// token issuer and the validated issuer always match. A real deployment sets a fixed URL.
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

// Seed sample data for local development only.
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

// The form-filler publishes to `form-submissions` and SubmissionConsumerService reads it.
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
