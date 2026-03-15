using StackExchange.Redis;

namespace Medinilla.RealTime.Redis;

public sealed class RedisMessenger : IRealTimeMessenger
{
    private readonly ISubscriber _connection;
    private readonly Dictionary<string, ChannelMessageQueue> _channels;

    public RedisMessenger(ConnectionMultiplexer mux)
    {
        _connection = mux.GetSubscriber();
        _channels = new Dictionary<string, ChannelMessageQueue>();
    }

    private void AssertChannel(string channelName, bool present = false)
    {
        if (present)
        {
            if (_channels.ContainsKey(channelName))
            {
                throw new NullReferenceException($"Redis connection channel '{channelName}' is already present.");
            }
        }
        else
        {
            if (_channels is null)
            {
                throw new NullReferenceException("Redis connection channel is not setup.");
            }
        }
    }

    public async Task DestroyChannel(string channelName)
    {
        await _connection.UnsubscribeAsync(new RedisChannel(channelName, RedisChannel.PatternMode.Auto));
    }

    public object GetChannel(string channelName)
    {
        AssertChannel(channelName);
        return _channels[channelName];
    }

    public string GetCommunicationProviderName() => "Redis";

    public async Task RegisterChannel(string channelName)
    {
        AssertChannel(channelName, true);
        var channel = await _connection.SubscribeAsync(new RedisChannel(channelName, RedisChannel.PatternMode.Auto));
        _channels.Add(channelName, channel);
    }

    public async Task RegisterHandler(string channelName, Func<object, Task> handler)
    {
        await Task.Run(() =>
        {
            AssertChannel(channelName);
            var channel = (ChannelMessageQueue)GetChannel(channelName);

            channel!.OnMessage(async (m) =>
            {
                var base64 = m.Message.ToString();
                await handler(Convert.FromBase64String(base64));
            });
        });
    }

    public async Task SendMessage(string channel, byte[] message)
    {
        AssertChannel(channel);
        await _connection.PublishAsync(new RedisChannel(channel, RedisChannel.PatternMode.Auto), new RedisValue(Convert.ToBase64String(message)));
    }
}
