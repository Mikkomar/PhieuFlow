var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");
var hubDb = sql.AddDatabase("HubDatabase", "PhieuFlowHub");

var migrations = builder.AddProject<Projects.PhieuFlow_MigrationService>("migrations")
    .WithReference(hubDb)
    .WaitFor(hubDb);

var hubBuilder = builder.AddProject<Projects.PhieuFlow_Hub>("hub")
    .WithReference(hubDb)
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
    .WaitFor(hub)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
