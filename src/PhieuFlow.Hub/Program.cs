using PhieuFlow.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<HubDbContext>("HubDatabase");

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
