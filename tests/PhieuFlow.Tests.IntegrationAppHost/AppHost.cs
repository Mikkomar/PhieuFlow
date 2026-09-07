// Minimal Aspire topology for the integration-sql test tier: a SQL Server container and
// the MigrationService worker, nothing else. PhieuFlow.Tests.Integration drives this via
// Aspire.Hosting.Testing; the Hub itself is hosted in-process by the test project.

var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddSqlServer("sql")
    .AddDatabase("HubDatabase", "PhieuFlowHub");

builder.AddProject<Projects.PhieuFlow_MigrationService>("migrations")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
