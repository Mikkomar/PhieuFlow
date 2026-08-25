var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");
var hubDb = sql.AddDatabase("HubDatabase", "PhieuFlowHub");

var migrations = builder.AddProject<Projects.PhieuFlow_MigrationService>("migrations")
    .WithReference(hubDb)
    .WaitFor(hubDb);

builder.AddProject<Projects.PhieuFlow_Hub>("hub")
    .WithReference(hubDb)
    .WaitForCompletion(migrations)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.PhieuFlow_FormBuilder>("formbuilder")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
