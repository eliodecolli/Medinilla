using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Medinilla.RealTime.Rabbit;

public sealed class RabbitMQMessenger : IRealTimeMessenger
{
    private ConnectionFactory _connectionFactory;
    private IConnection _connection;

    // eventually we need to set a max limit for concurrent channels.
    // to do this, we need to offload channel storage and retrieval to a separate class,
    // let's be fancy and call it ChannelPool.
    private Dictionary<string, IChannel> _channels;

    public RabbitMQMessenger(string connectionUri)
    {
        _connectionFactory = new ConnectionFactory();
        _channels = new();

        _connectionFactory.Uri = new Uri(connectionUri);
        _connection = _connectionFactory.CreateConnectionAsync().Result;
    }

    public async Task DestroyChannel(string channelName)
    {
        if (_channels.TryGetValue(channelName, out IChannel channel))
        {
            await channel.CloseAsync().ConfigureAwait(false);
            await channel.DisposeAsync().ConfigureAwait(false);
            _channels.Remove(channelName);
        }
    }

    public string GetCommunicationProviderName() => "RabbitMQ";

    public async Task RegisterChannel(string channelName)
    {
        var channel = await _connection.CreateChannelAsync(new CreateChannelOptions(true, false)).ConfigureAwait(false);
        await channel.QueueDeclareAsync(channelName, exclusive: false);

        _channels.Add(channelName, channel);
    }

    public async Task RegisterHandler(string channelName, Func<object, Task> handler)
    {
        if (_channels.TryGetValue(channelName, out IChannel channel))
        {
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (object model, BasicDeliverEventArgs ea) => await handler(new Dictionary<string, object>()
            {
                { "model", model },
                { "ea", ea }
            });
            await channel.BasicConsumeAsync(channelName, true, consumer);
        }
    }

    public async Task SendMessage(string channelName, byte[] message)
    {
        if (_channels.TryGetValue(channelName, out IChannel channel))
        {
            await channel.BasicPublishAsync("", channelName, message).ConfigureAwait(false);
        }
    }
}
