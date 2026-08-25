var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");
var hubDb = sql.AddDatabase("HubDatabase", "PhieuFlowHub");

builder.AddProject<Projects.PhieuFlow_Hub>("hub")
    .WithReference(hubDb)
    .WaitFor(hubDb)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.PhieuFlow_FormBuilder>("formbuilder")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
