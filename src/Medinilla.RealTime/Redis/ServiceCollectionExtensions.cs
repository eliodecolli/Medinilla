using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Medinilla.RealTime.Redis;

internal static class ServiceCollectionExtensions
{
    static string ProducerName => "medinilla.producer";
    static string ConsumerName => "medinilla.consumer";
    
    internal static IServiceCollection AddMedinillaRedis(this IServiceCollection services)
    {
        var builder = new ConfigurationBuilder();
        using var stream = typeof(ServiceCollectionExtensions).Assembly.GetManifestResourceStream("Medinilla.RealTime.appsettings.json");

        builder.AddJsonStream(stream);
        var config = builder.Build();

        var redisUri = config.GetSection("Redis")["Uri"] ?? "";

        var producerOptions = ConfigurationOptions.Parse(redisUri);
        producerOptions.ClientName = $"{ProducerName}-{Random.Shared.NextInt64()}";

        var consumerOptions = ConfigurationOptions.Parse(redisUri);
        consumerOptions.ClientName = $"{ConsumerName}-{Random.Shared.NextInt64()}";

        services.AddKeyedSingleton<ConnectionMultiplexer>(producerOptions.ClientName, (_, _) => ConnectionMultiplexer.Connect(producerOptions));
        services.AddKeyedSingleton<ConnectionMultiplexer>(consumerOptions.ClientName, (_, _) => ConnectionMultiplexer.Connect(consumerOptions));

        services.AddSingleton<ISender>(sp => new RedisSender(sp.GetRequiredKeyedService<ConnectionMultiplexer>(producerOptions.ClientName)));

        services.AddSingleton<IReceiver>(sp => new RedisReceiver(sp.GetRequiredKeyedService<ConnectionMultiplexer>(consumerOptions.ClientName)));

        return services;
    }
}