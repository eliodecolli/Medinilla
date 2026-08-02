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
}