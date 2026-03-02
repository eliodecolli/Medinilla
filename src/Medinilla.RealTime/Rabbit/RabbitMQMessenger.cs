using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Medinilla.RealTime.Rabbit;

public sealed class RabbitMQMessenger
{
    private IConnection _connection;
    private ConnectionFactory _factory;

    // eventually we need to set a max limit for concurrent channels.
    // to do this, we need to offload channel storage and retrieval to a separate class,
    // let's be fancy and call it ChannelPool.
    private Dictionary<string, IChannel> _channels;
    private Dictionary<string, string> _routingKeys;

    public RabbitMQMessenger(string connectionString)
    {
        _channels = new Dictionary<string, IChannel>();
        _routingKeys = new Dictionary<string, string>();

        _factory = new ConnectionFactory();
        _factory.Uri = new Uri(connectionString);

        _connection = _factory.CreateConnectionAsync().Result;
    }

    private void SetChannel(string name, IChannel channel, string routingKey)
    {
        _channels.Add(name, channel);
        _routingKeys.Add(name, routingKey);
    }

    private string GetRoutingKey(string channelName)
    {
        return _routingKeys[channelName];
    }

    public object GetChannel(string channelName)
    {
        return _channels[channelName];
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

    public string QueueName => "medinilla_core_queue";

    public async Task RegisterChannel(string channelName, string clientIdentifier)
    {
        var channel = await _connection.CreateChannelAsync(new CreateChannelOptions(true, false)).ConfigureAwait(false);

        await channel.ExchangeDeclareAsync(channelName, ExchangeType.Topic, true, false);

        await channel.QueueDeclareAsync(QueueName, exclusive: false, durable: true, autoDelete: false);
        await channel.QueueBindAsync(QueueName, channelName, clientIdentifier);

        SetChannel(channelName, channel, clientIdentifier);
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
            await channel.BasicConsumeAsync(QueueName, true, consumer);
        }
    }

    public async Task SendMessage(string channelName, byte[] message)
    {
        if (_channels.TryGetValue(channelName, out IChannel channel))
        {
            var routingKey = GetRoutingKey(channelName);
            await channel.BasicPublishAsync(channelName, routingKey, message).ConfigureAwait(false);
        }
    }

    public async Task SendMessage(string channelName, string routingKey, byte[] message)
    {
        if (_channels.TryGetValue(channelName, out IChannel channel))
        {
            await channel.BasicPublishAsync(channelName, routingKey, message).ConfigureAwait(false);
        }
    }
}
