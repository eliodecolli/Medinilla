using Medinilla.RealTime.PubNub;
using Medinilla.RealTime.Rabbit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddScoped<IRealTimeMessenger, PubNubClient>();
        services.AddScoped<IRealTimeMessenger, RabbitMQMessenger>(provider =>
        {
            var connectionUri = config.GetSection("RabbitMQ")["Uri"] ?? "";
            return new RabbitMQMessenger(connectionUri);
        });

        return services;
    }
}