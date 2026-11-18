using Google.Protobuf;
using Medinilla.Core.SharedContracts.Events;
using Medinilla.Infrastructure.Events;
using Medinilla.RealTime.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Medinilla.Core.Service.Events;

/// <summary>
/// Publishes completed <see cref="MedinillaHttpEvent"/> wrappers to the Redis events
/// stream consumed by the webhook delivery worker. The generated protobuf is the
/// stream payload verbatim; no JSON or base64 conversion is applied.
/// </summary>
internal sealed class RedisChargerEventPublisher(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<RedisChargerEventPublisher> logger) : IChargerEventPublisher
{
    private readonly string _stream = configuration["Webhooks:EventsStream"]
        ?? WebhookEventTransport.EventsStream;

    private readonly string _payloadField = configuration["Webhooks:PayloadField"]
        ?? WebhookEventTransport.PayloadField;

    public async Task PublishAsync(
        MedinillaHttpEvent httpEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpEvent);

        var multiplexer = serviceProvider.GetRequiredKeyedService<ConnectionMultiplexer>(
            RedisUtils.ProducerConnectionMultiplexer);

        var database = multiplexer.GetDatabase();
        var payload = httpEvent.ToByteArray();

        await database
            .StreamAddAsync(_stream, _payloadField, payload)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        logger.LogDebug(
            "Accepted webhook event {EventType} {EventId} into stream {Stream}.",
            httpEvent.EventType,
            httpEvent.EventId,
            _stream);
    }
}
