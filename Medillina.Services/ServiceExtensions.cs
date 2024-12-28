using Medinilla.Core.Interfaces;
using Medinilla.Core.Logic.Authorization;
using Medinilla.Core.Logic.Authorization.Algorithms;
using Medinilla.Services.Actions;
using Medinilla.Services.Actions.Ocpp201;
using Medinilla.Services.Interfaces;
using Medinilla.Services.v1;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<IAuthAlgorithm, DefaultAuthorization>();
        services.AddScoped<IAuthAlgorithm, LocationCheckAlgo>();
        services.AddScoped<IAuthAlgorithm, DateRangeCheckAlgo>();
        services.AddScoped<IAuthAlgorithm, CreditCheckAlgo>();
        //...add more
    }

    public static void AddMedinillaServices(this IServiceCollection serviceCollection)
    {
        AddOcppActions(serviceCollection);
        AddAuthAlgos(serviceCollection);

        serviceCollection.AddScoped<IOcppActionsFactory, OcppActionsFactory>();
        serviceCollection.AddSingleton<IOcppCallRouter, OcppCallRouter>();
        serviceCollection.AddScoped<AuthorizationAlgorithmFactory>();
    }
}
