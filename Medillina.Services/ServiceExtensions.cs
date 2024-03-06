using Medinilla.Services.v1;
using Medinilla.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Medinilla.Services.Actions;
using Medinilla.Services.Actions.Ocpp201;

namespace Medinilla.Services;

public static class ServiceExtensions
{
    private static void AddOcppActions(IServiceCollection services)
    {
        services.AddScoped<IOcppAction, BootNotificationAction>();
        //...add more
    }

    public static void AddMedinillaServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IOcppActionsFactory, OcppActionsFactory>();
        serviceCollection.AddSingleton<IMedinillaAuthentication, MedinillaAuthentication>();
        serviceCollection.AddScoped<IOcppCallRouter, OcppCallRouter>();
        serviceCollection.AddScoped<IBasicWebSocketDigestionService, WebSocketDigestionService>();

        AddOcppActions(serviceCollection);
    }
}
