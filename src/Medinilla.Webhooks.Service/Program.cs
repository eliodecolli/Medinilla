using Medinilla.Webhooks.Service;
using Medinilla.Webhooks.Service.Configuration;
using Medinilla.Webhooks.Service.Delivery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

var webhookOptions = builder.Configuration
    .GetSection(WebhookOptions.SectionName)
    .Get<WebhookOptions>() ?? new WebhookOptions();

builder.Services.AddSingleton(webhookOptions);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<WebhookSecretResolver>();
builder.Services.AddSingleton<WebhookSubscriptionRegistry>();
builder.Services.AddSingleton<WebhookDispatcher>();

builder.Services
    .AddHttpClient(WebhookDispatcher.HttpClientName, client =>
    {
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, webhookOptions.HttpTimeoutSeconds));
    })
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        // Endpoints are deployment-controlled; never follow redirects.
        AllowAutoRedirect = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    });

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var configuration = ConfigurationOptions.Parse(webhookOptions.RedisUri);
    configuration.AbortOnConnectFail = false;
    configuration.ClientName = $"medinilla-webhooks-{Environment.ProcessId}";
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddHostedService<WebhookDeliveryWorker>();

var host = builder.Build();
await host.RunAsync();
