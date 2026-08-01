using Medinilla.RealTime.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Medinilla.RealTime;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRealTimeServices(this IServiceCollection services)
    {
        var builder = new ConfigurationBuilder();
        using var stream = typeof(ServiceCollectionExtensions).Assembly.GetManifestResourceStream("Medinilla.RealTime.appsettings.json");

        builder.AddJsonStream(stream);
        var config = builder.Build();

        var redisUri = config.GetSection("Redis")["Uri"] ?? "";

        services.AddSingleton(_ => ConnectionMultiplexer.Connect(redisUri));

        services.AddSingleton<ISender>(sp => new RedisQueue(sp.GetRequiredService<ConnectionMultiplexer>()));

        // Scoped: each consumer (one per WS session, one in CoreInterfaceCommunication)
        // gets its own instance so it can run its own BRPOP loop on its own queue.
        services.AddScoped<IReceiver>(sp => new RedisQueue(sp.GetRequiredService<ConnectionMultiplexer>()));

        return services;
    }
}
