using Medinilla.RealTime.PubNub;
using Medinilla.RealTime.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Medinilla.RealTime;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRealTimeServices(this IServiceCollection services)
    {
        // Load the embedded configuration first
        var builder = new ConfigurationBuilder();
        using var stream = typeof(ServiceCollectionExtensions).Assembly.GetManifestResourceStream("Medinilla.RealTime.appsettings.json");

        builder.AddJsonStream(stream);
        var config = builder.Build();

        // Fix: Configure options correctly by binding the section
        var pubnubSection = config.GetSection("Pubnub");
        services.Configure<PubnubConfiguration>(options =>
        {
            options.PublishKey = pubnubSection["PublishKey"];
            options.SubscribeKey = pubnubSection["SubscribeKey"];
            options.UserId = pubnubSection["UserId"];
        });

        services.AddTransient<ICommunicationProvider, CommunicationProvider>();

        services.AddSingleton<IRealTimeMessenger, PubNubClient>();

        services.AddSingleton(_ =>
        {
            var connectionUri = config.GetSection("Redis")["Uri"] ?? "";
            return ConnectionMultiplexer.Connect(connectionUri);
        });

        services.AddScoped<IRealTimeMessenger>(provider =>
        {
            var mux = provider.GetService<ConnectionMultiplexer>();
            return new RedisMessenger(mux);
        });

        var redisUri = config.GetSection("Redis")["Uri"] ?? "";

        // Outbound (RPUSH): backed by the shared singleton multiplexer — safe for non-blocking commands.
        services.AddKeyedSingleton<IRedisQueue>("outbound", (sp, _) =>
            new RedisQueue(sp.GetRequiredService<ConnectionMultiplexer>()));

        // Inbound (BRPOP): each consumer gets its own dedicated connection so blocking
        // BRPOP never starves RPUSH commands on the shared connection.
        services.AddKeyedTransient<IRedisQueue>("inbound", (_, _) =>
            new RedisQueue(redisUri));

        return services;
    }
}