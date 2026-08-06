using Medinilla.Core;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Service.Communication;
using Medinilla.Core.Service.Interfaces;
using Medinilla.Core.Service.Types;
using Medinilla.Core.v1;
using Medinilla.DataAccess;
using Medinilla.Infrastructure;
using Medinilla.RealTime;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Medinilla.Core.gRPC.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Medinilla.Core.Service;

var builder = WebApplication.CreateBuilder(args);

var cfgBuilder = new ConfigurationBuilder();
using var stream = typeof(Program).Assembly.GetManifestResourceStream("Medinilla.Core.Service.settings.json");

cfgBuilder.AddJsonStream(stream);
var config = cfgBuilder.Build();

builder.Configuration.AddConfiguration(config);

builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.IncludeScopes = false;
    options.TimestampFormat = "[HH:mm:ss]: ";
});

builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);


builder.Services.AddMedinillaInfrastructure();
builder.Services.AddMedinillaDataAccess();
builder.Services.AddMedinillaServices();
builder.Services.AddRealTimeServices();

builder.Services.AddSingleton(CommunicationSettings.FromSettingsFile("settings.json"));
builder.Services.AddScoped<IOcppRequestDispatcher, OcppRequestDispatcher>();
builder.Services.AddSingleton<MedinillaGrpc>();
builder.Services.AddScoped<BaseOcppRoutingTable, RedisRoutingTable>();
builder.Services.AddSingleton<IInterfaceCommunication, CoreInterfaceCommunication>();

builder.Services.AddHostedService<InboundWorker>();

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<MedinillaGrpc>();

await app.RunAsync();
