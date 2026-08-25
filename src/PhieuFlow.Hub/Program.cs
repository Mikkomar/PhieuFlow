using Microsoft.EntityFrameworkCore;
using PhieuFlow.Hub.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<HubDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("HubDatabase")
        ?? throw new InvalidOperationException("Missing connection string 'HubDatabase'.")));

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
