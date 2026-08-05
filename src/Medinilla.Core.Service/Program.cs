using Medinilla.Core;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Service.Communication;
using Medinilla.Core.Service.Interfaces;
using Medinilla.Core.Service.Types;
using Medinilla.Core.v1;
using Medinilla.DataAccess;
using Medinilla.Infrastructure;
using Medinilla.RealTime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var hostApplicationBuilder = Host.CreateApplicationBuilder(args);

var builder = new ConfigurationBuilder();
using var stream = typeof(Program).Assembly.GetManifestResourceStream("Medinilla.Core.Service.settings.json");

builder.AddJsonStream(stream);
var config = builder.Build();

hostApplicationBuilder.Configuration.AddConfiguration(config);

hostApplicationBuilder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.IncludeScopes = false;
    options.TimestampFormat = "[HH:mm:ss]: ";
});

// Filter noisy namespaces
hostApplicationBuilder.Logging.SetMinimumLevel(LogLevel.Debug);
hostApplicationBuilder.Logging.AddFilter("Microsoft", LogLevel.Warning);


hostApplicationBuilder.Services.AddMedinillaInfrastructure();
hostApplicationBuilder.Services.AddMedinillaDataAccess();
hostApplicationBuilder.Services.AddMedinillaServices();
hostApplicationBuilder.Services.AddRealTimeServices();

hostApplicationBuilder.Services.AddSingleton(CommunicationSettings.FromSettingsFile("settings.json"));
hostApplicationBuilder.Services.AddScoped<IOcppRequestDispatcher, OcppRequestDispatcher>();
hostApplicationBuilder.Services.AddScoped<BaseOcppRoutingTable, RedisRoutingTable>();
hostApplicationBuilder.Services.AddSingleton<IInterfaceCommunication, CoreInterfaceCommunication>();

using var host = hostApplicationBuilder.Build();

var interfaceComms = host.Services.GetRequiredService<IInterfaceCommunication>();
var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

await interfaceComms.Run(lifetime.ApplicationStopping);

await host.WaitForShutdownAsync();