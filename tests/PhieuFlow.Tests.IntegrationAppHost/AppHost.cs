// Minimal Aspire topology for the integration-sql tier: a SQL Server container and the
// MigrationService worker. PhieuFlow.Tests.Integration drives it. The Hub runs in-process.

var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddSqlServer("sql")
    .AddDatabase("HubDatabase", "PhieuFlowHub");

builder.AddProject<Projects.PhieuFlow_MigrationService>("migrations")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
