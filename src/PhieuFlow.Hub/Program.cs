using PhieuFlow.Hub.Endpoints;
using PhieuFlow.Persistence;
using PhieuFlow.Persistence.Repositories;
using PhieuFlow.Persistence.UnitOfWork;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<HubDbContext>("HubDatabase");

builder.Services.AddScoped<IFormRepository, FormRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapFormEndpoints();

app.Run();
