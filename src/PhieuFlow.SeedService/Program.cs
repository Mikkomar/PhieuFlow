using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhieuFlow.Persistence;
using PhieuFlow.SeedService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<HubDbContext>("HubDatabase");

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
