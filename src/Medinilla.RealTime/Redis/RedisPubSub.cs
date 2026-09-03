using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Medinilla.RealTime.Redis;

public class RedisPubSub(ConnectionMultiplexer mux, ILogger<RedisPubSub> log) : IPubSub
{
    readonly ISubscriber _subscriber = mux.GetSubscriber();
    readonly Dictionary<string, Action<string, string>> _topics = new();

    private void AssertSubscriber()
    {
        if (_subscriber is null) throw new InvalidOperationException("Subscriber not initialized");
    }

    private void HandleMessage(RedisChannel channel, RedisValue value)
    {
        if (channel.IsNullOrEmpty)
        {
            log.LogError("Signaled handler from invalid channel: NULL");
            return;
        }
        
        var topic = value.ToString();
        
        log.LogInformation("Received message from topic: {topic}", topic);

        if (!value.HasValue) return;
        if (!_topics.TryGetValue(topic, out Action<string, string>? handler)) return;
        
        handler(topic, value.ToString());
    }

    public async Task ListenForTopic(string topic, Action<string, string> callback)
    {
        AssertSubscriber();
        if (!_topics.TryAdd(topic, callback))
        {
            _topics[topic] = callback;
        }

        await _subscriber.SubscribeAsync(new RedisChannel(topic, RedisChannel.PatternMode.Literal), HandleMessage);
    }

    public async Task Publish(string topic, string message)
    {
        await _subscriber.PublishAsync(new  RedisChannel(topic, RedisChannel.PatternMode.Literal), message);
    }
}