using Medinilla.Services.v1;
using Medinilla.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Medinilla.Services.Actions;
using Medinilla.Services.Actions.Ocpp201;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Logic.Authorization.Algorithms;
using Medinilla.Core.Logic.Authorization;

namespace Medinilla.Services;

public static class ServiceExtensions
{
    private static void AddOcppActions(IServiceCollection services)
    {
        services.AddScoped<IOcppAction, BootNotificationAction>();
        services.AddScoped<IOcppAction, HeartbeatAction>();
        services.AddScoped<IOcppAction, SecurityEventNotificationAction>();
        services.AddScoped<IOcppAction, AuthorizeAction>();
        services.AddScoped<IOcppAction, TransactionEventAction>();
        services.AddScoped<IOcppAction, StatusNotificationAction>();
        //...add more
    }

    private static void AddAuthAlgos(IServiceCollection services)
    {
        services.AddScoped<IAuthAlgorithm, EvseCheckAlgo>();
        services.AddScoped<IAuthAlgorithm, ExpiryCheckAlgo>();
        //...add more
    }

    public static void AddMedinillaServices(this IServiceCollection serviceCollection)
    {
        AddOcppActions(serviceCollection);
        AddAuthAlgos(serviceCollection);

        serviceCollection.AddSingleton<IWSDigestionServiceCollection, WSDigestionServiceCollection>();
        serviceCollection.AddTransient<IOcppActionsFactory, OcppActionsFactory>();
        serviceCollection.AddScoped<IOcppCallRouter, OcppCallRouter>();
        serviceCollection.AddScoped<IBasicWebSocketDigestionService, WebSocketDigestionService>();
        serviceCollection.AddScoped<IOcppMessageParser, OcppMessageParser>();
        serviceCollection.AddScoped<AuthorizationAlgorithmFactory>();
    }
}
