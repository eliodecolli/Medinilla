using Akka.DependencyInjection;
using Akka.Hosting;
using Medinilla.Core;
using Medinilla.Core.Service.Communication;
using Medinilla.Core.Service.Communication.Actors;
using Medinilla.Core.Service.Interfaces;
using Medinilla.Core.Service.Types;
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

hostApplicationBuilder.Services.AddSingleton<IConfiguration>(config);
hostApplicationBuilder.Configuration.AddConfiguration(config);

hostApplicationBuilder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.IncludeScopes = false;
    options.TimestampFormat = "HH:mm:ss ";
});

// Filter noisy namespaces
hostApplicationBuilder.Logging.SetMinimumLevel(LogLevel.Information);
hostApplicationBuilder.Logging.AddFilter("Microsoft", LogLevel.Warning);
hostApplicationBuilder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);


hostApplicationBuilder.Services.AddMedinillaInfrastructure();
hostApplicationBuilder.Services.AddMedinillaDataAccess();
hostApplicationBuilder.Services.AddMedinillaServices();
hostApplicationBuilder.Services.AddRealTimeServices();
hostApplicationBuilder.Services.AddScoped<IInterfaceCommunication, CoreInterfaceCommunication>();

hostApplicationBuilder.Services.AddAkka("medinilla-core-akka", builder =>
{
    builder.WithActors((system, registry, resolver) =>
    {
        var coordinator = system.ActorOf(resolver.Props<Coordinator>(DependencyResolver.For(system)), "ocpp-coordinator");
        registry.Register<Coordinator>(coordinator);
    });
});

using var host = hostApplicationBuilder.Build();

var interfaceComms = host.Services.GetRequiredService<IInterfaceCommunication>();
await interfaceComms.Run(CommunicationSettings.FromSettingsFile("settings.json"));

await host.RunAsync();