using Medinilla.Core.Actions;
using Medinilla.Core.Actions.Ocpp201;
using Medinilla.Core.Commands;
using Medinilla.Core.Commands.Ocpp201;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.Logic.Authorization;
using Medinilla.Core.Logic.Authorization.Algorithms;
using Medinilla.Core.v1;
using Medinilla.Core.v1.Services;
using Medinilla.Core.v1.Transactions;
using Microsoft.Extensions.DependencyInjection;

namespace Medinilla.Core;

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

    private static void AddOcppChargerCommands(IServiceCollection services)
    {
        services.AddScoped<IOcppChargerCommand, SetVariablesCommand>();
        services.AddScoped<IOcppChargerCommand, GetVariablesCommand>();
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
        AddOcppChargerCommands(serviceCollection);
        AddAuthAlgos(serviceCollection);

        // add services
        serviceCollection.AddScoped<IChargingStationBootingService, ChargingStationBooting>();
        serviceCollection.AddScoped<IRouterServices, RouterServices>();
        serviceCollection.AddScoped<IIdTokenService, IdTokenService>();
        serviceCollection.AddScoped<ITariffService, TariffService>();

        serviceCollection.AddScoped<IOcppActionsFactory, OcppActionsFactory>();
        serviceCollection.AddScoped<IOcppChargerCommandFactory, OcppChargerCommandsFactory>();
        serviceCollection.AddScoped<IOcppCallRouter, OcppCallRouter>();
        serviceCollection.AddScoped<AuthorizationAlgorithmFactory>();
        serviceCollection.AddScoped<ConsumptionService>();
    }
}
