using Medinilla.RealTime.Redis;
using Microsoft.Extensions.DependencyInjection;

namespace Medinilla.RealTime;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRealTimeServices(this IServiceCollection services)
    {
        services.AddMedinillaRedis();
        return services;
    }

    /// <summary>Requires <see cref="AddRealTimeServices"/> to have run first.</summary>
    public static IServiceCollection AddWebSocketRoutingTable(this IServiceCollection services)
    {
        services.AddMedinillaRoutingTable();
        return services;
    }

    /// <summary>Requires <see cref="AddRealTimeServices"/> to have run first.</summary>
    public static IServiceCollection AddSubscriptionReceiver(this IServiceCollection services)
    {
        services.AddMedinillaSubscriptionReceiver();
        return services;
    }
    
    public static IServiceCollection AddPubSub(this IServiceCollection services)
    {
        services.AddMedinillaPubSub();
        return services;
    }
}
