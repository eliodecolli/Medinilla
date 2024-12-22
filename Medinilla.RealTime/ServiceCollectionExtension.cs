using Microsoft.Extensions.DependencyInjection;
using Medinilla.RealTime.PubNub;
using Microsoft.Extensions.Configuration;

namespace Medinilla.RealTime;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRealTimeServices(this IServiceCollection services)
    {
        // Load the embedded configuration first
        var builder = new ConfigurationBuilder();
        using (var stream = typeof(ServiceCollectionExtensions).Assembly
            .GetManifestResourceStream("Medinilla.RealTime.appsettings.json"))
        {
            if (stream is not null)
            {
                builder.AddJsonStream(stream);
            }
        }
        var config = builder.Build();
        
        // Fix: Configure options correctly by binding the section
        var pubnubSection = config.GetSection("Pubnub");
        services.Configure<PubnubConfiguration>(options => {
            options.PublishKey = pubnubSection["PublishKey"];
            options.SubscribeKey = pubnubSection["SubscribeKey"];
            options.UserId = pubnubSection["UserId"];
        });

        services.AddScoped<IRealTimeMessenger, PubNubClient>();

        return services;
    }
}