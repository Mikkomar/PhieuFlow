using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhieuFlow.Hub.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<HubDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("HubDatabase")
        ?? throw new InvalidOperationException("Missing connection string 'HubDatabase'.")));

var host = builder.Build();
host.Run();
