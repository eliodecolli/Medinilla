using Medinilla.Core.Service;
using Medinilla.Core.Service.Communication;
using Medinilla.Core.Service.Interfaces;
using Medinilla.DataAccess;
using Medinilla.Infrastructure;
using Medinilla.RealTime;
using Medinilla.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var hostApplicationBuilder = Host.CreateApplicationBuilder(args);

var builder = new ConfigurationBuilder();
using var stream = typeof(CoreServiceHost).Assembly.GetManifestResourceStream("Medinilla.Core.Service.settings.json");

builder.AddJsonStream(stream);
var config = builder.Build();

hostApplicationBuilder.Configuration.AddConfiguration(config);

hostApplicationBuilder.Services.AddLogging();
hostApplicationBuilder.Logging.ClearProviders();
hostApplicationBuilder.Logging.AddSimpleConsole();
hostApplicationBuilder.Logging.SetMinimumLevel(LogLevel.Trace);

hostApplicationBuilder.Services.AddMedinillaInfrastructure();
hostApplicationBuilder.Services.AddMedinillaDataAccess();
hostApplicationBuilder.Services.AddMedinillaServices();
hostApplicationBuilder.Services.AddRealTimeServices();
hostApplicationBuilder.Services.AddHostedService<CoreServiceHost>();
hostApplicationBuilder.Services.AddScoped<IInterfaceCommunication, CoreInterfaceCommunication>();

using var host = hostApplicationBuilder.Build();
await host.RunAsync();