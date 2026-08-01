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

        // Outbound (RPUSH): shared singleton multiplexer — safe for non-blocking commands.
        services.AddKeyedSingleton<IMessageQueue>("outbound", (sp, _) =>
            new RedisQueue(sp.GetRequiredService<ConnectionMultiplexer>()));

        // Inbound (BRPOP): each consumer gets its own dedicated connection so blocking
        // BRPOP never starves RPUSH commands on the shared connection.
        services.AddKeyedTransient<IMessageQueue>("inbound", (_, _) =>
            new RedisQueue(redisUri));

        return services;
    }
}
