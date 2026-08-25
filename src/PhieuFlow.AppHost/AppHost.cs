var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.PhieuFlow_Hub>("hub")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.PhieuFlow_FormBuilder>("formbuilder")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
