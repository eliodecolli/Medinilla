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

        static ConfigurationOptions OptionsFor(string uri, string clientName)
        {
            var options = ConfigurationOptions.Parse(uri);
            options.ClientName = $"{clientName}-{Random.Shared.NextInt64()}";
            return options;
        }

        var producerOptions = OptionsFor(redisUri, RedisUtils.ProducerConnectionMultiplexer);
        var consumerOptions = OptionsFor(redisUri, RedisUtils.ConsumerConnectionMultiplexer);
        var subscriptionOptions = OptionsFor(redisUri, RedisUtils.SubscriptionConnectionMultiplexer);

        services.AddKeyedSingleton(RedisUtils.ProducerConnectionMultiplexer,
            (_, _) => ConnectionMultiplexer.Connect(producerOptions));
        services.AddKeyedSingleton(RedisUtils.ConsumerConnectionMultiplexer,
            (_, _) => ConnectionMultiplexer.Connect(consumerOptions));

        // Lazy: only opened if something actually resolves it, so Core.Service (which
        // has no subscription receiver) never pays for this connection.
        services.AddKeyedSingleton(RedisUtils.SubscriptionConnectionMultiplexer,
            (_, _) => ConnectionMultiplexer.Connect(subscriptionOptions));

        services.AddScoped<ISender>(sp => new RedisSender(
            sp.GetRequiredKeyedService<ConnectionMultiplexer>(RedisUtils.ProducerConnectionMultiplexer))
        );

        services.AddScoped<IReceiver>(sp => new RedisReceiver(
            sp.GetRequiredKeyedService<ConnectionMultiplexer>(RedisUtils.ConsumerConnectionMultiplexer),
            sp.GetRequiredService<ILogger<IReceiver>>())
        );

        return services;
    }

    // The routing table and the subscription receiver are singletons, so they cannot
    // take the scoped ISender/IReceiver — they build their own handles off the
    // singleton multiplexers instead.
    internal static IServiceCollection AddMedinillaRoutingTable(this IServiceCollection services)
    {
        services.AddSingleton<IWebSocketRoutingTable>(sp => new RedisWebSocketRoutingTable(
            sp.GetRequiredKeyedService<ConnectionMultiplexer>(RedisUtils.ProducerConnectionMultiplexer).GetDatabase(),
            sp.GetRequiredService<ILogger<RedisWebSocketRoutingTable>>()));

        return services;
    }

    // Gets its own multiplexer: the dispatch loop sits in a blocking BLPOP permanently,
    // which would stall every other command sharing that connection.
    internal static IServiceCollection AddMedinillaSubscriptionReceiver(this IServiceCollection services)
    {
        services.AddSingleton<ISubscriptionReceiver>(sp => new RedisSubscriptionReceiver(
            new RedisReceiver(
                sp.GetRequiredKeyedService<ConnectionMultiplexer>(RedisUtils.SubscriptionConnectionMultiplexer),
                sp.GetRequiredService<ILogger<IReceiver>>()),
            sp.GetRequiredService<ILogger<RedisSubscriptionReceiver>>()));

        return services;
    }

    internal static IServiceCollection AddMedinillaPubSub(this IServiceCollection services)
    {
        services.AddSingleton<IPubSub>(sp => new RedisPubSub(
            sp.GetRequiredKeyedService<ConnectionMultiplexer>(RedisUtils.SubscriptionConnectionMultiplexer),
            sp.GetRequiredService<ILogger<RedisPubSub>>()));

        return services;
    }
}