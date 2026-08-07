using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Medinilla.RealTime.Redis;

internal static class ServiceCollectionExtensions
{
    
    internal static IServiceCollection AddMedinillaRedis(this IServiceCollection services)
    {
        var builder = new ConfigurationBuilder();
        using var stream = typeof(ServiceCollectionExtensions).Assembly.GetManifestResourceStream("Medinilla.RealTime.appsettings.json");

        builder.AddJsonStream(stream);
        var config = builder.Build();

        var redisUri = config.GetSection("Redis")["Uri"] ?? "";

        var producerOptions = ConfigurationOptions.Parse(redisUri);
        producerOptions.ClientName = $"{RedisUtils.ProducerConnectionMultiplexer}-{Random.Shared.NextInt64()}";

        var consumerOptions = ConfigurationOptions.Parse(redisUri);
        consumerOptions.ClientName = $"{RedisUtils.ConsumerConnectionMultiplexer}-{Random.Shared.NextInt64()}";

        services.AddKeyedSingleton(RedisUtils.ProducerConnectionMultiplexer,
            (_, _) => ConnectionMultiplexer.Connect(producerOptions));
        services.AddKeyedSingleton(RedisUtils.ConsumerConnectionMultiplexer,
            (_, _) => ConnectionMultiplexer.Connect(consumerOptions));

        services.AddScoped<ISender>(sp => new RedisSender(
            sp.GetRequiredKeyedService<ConnectionMultiplexer>(RedisUtils.ProducerConnectionMultiplexer))
        );

        services.AddScoped<IReceiver>(sp => new RedisReceiver(
            sp.GetRequiredKeyedService<ConnectionMultiplexer>(RedisUtils.ConsumerConnectionMultiplexer),
            sp.GetRequiredService<ILogger<IReceiver>>())
        );

        return services;
    }
}