using Medinilla.Core.Service.Interfaces;
using Medinilla.Core.Service.Types;
using Medinilla.Core.SharedContracts.Comms;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Medinilla.Services.Interfaces;
using Medinilla.Core.SharedContracts.Comms.Ocpp;
using Google.Protobuf;
using Medinilla.Infrastructure.Interops;

namespace Medinilla.Core.Service.Communication;

internal class CoreInterfaceCommunication : IInterfaceCommunication
{
    private readonly TaskCompletionSource _runningTask;

    private CommunicationSettings _settings;
    private ConnectionFactory _connectionFactory;
    private IConnection _connection;
    private ILogger<CoreInterfaceCommunication> _logger;

    private IOcppCallRouter _ocppCallRouter;

    private Dictionary<string, ChannelSemaphore> _channels;

    public CoreInterfaceCommunication(ILogger<CoreInterfaceCommunication> logger, IOcppCallRouter router)
    {
        _connectionFactory = new ConnectionFactory();
        _logger = logger;
        _ocppCallRouter = router;
        _channels = new();
        _runningTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public async Task Connect(CommunicationSettings settings)
    {
        _settings = settings;
        _connectionFactory.Uri = new Uri(settings.RabbitUri);
        _connection = await _connectionFactory.CreateConnectionAsync().ConfigureAwait(false);
    }

    private async Task ConsumeOcppCall(AsyncEventingBasicConsumer consumer, BasicDeliverEventArgs ea, OcppMessage message)
    {
        try
        {
            _logger.LogInformation($"Received message on channel {ea.RoutingKey}");

            var result = await _ocppCallRouter.RouteOcppCall(message.Payload.ToByteArray(), message.ClientIdentifier);
            var response = new WampResult()
            {
                Result = result.Result is not null ? ByteString.CopyFrom(result.Result.ToByteArray()) : ByteString.Empty,
                Error = result.Error is not null ? ByteString.CopyFrom(result.Error.ToByteArray()) : ByteString.Empty,
                ReturnToCS = result.ReturnToCS
            };

            var finalResponse = new Comms()
            {
                MessageType = CommsMessageType.OcppResponse,
                Payload = response.ToByteString()
            };

            var channel = consumer.Channel;
            var responseChannel = CommsUtils.GetResponseChannelName(ea.RoutingKey);

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: responseChannel,
                body: finalResponse.ToByteArray()).ConfigureAwait(false);

            _logger.LogInformation($"Sent response to {responseChannel}");

        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing message on queue {ea.RoutingKey}: {ex.ToString()}");
        }
    }

    private async Task RunEvent(object model, BasicDeliverEventArgs ea)
    {
        var comms = Comms.Parser.ParseFrom(ea.Body.ToArray());

        switch (comms.MessageType)
        {
            case CommsMessageType.OcppRequest:
                await ConsumeOcppCall((model as AsyncEventingBasicConsumer)!, ea, OcppMessage.Parser.ParseFrom(ea.Body.ToArray()));
                break;

            case CommsMessageType.OcppResponse:
            // implement
            default:
                break;
        }
    }

    private async Task ConsumeCommunicationSignal(object model, BasicDeliverEventArgs ea)
    {
        var payload = CommunicationChannelSignal.Parser.ParseFrom(ea.Body.ToArray());
        _logger.LogInformation($"Received signal: {payload.ChannelName} Flag: {payload.Flag}");

        switch (payload.Flag)
        {
            case CommsFlag.Set:
                var channel = await _connection.CreateChannelAsync().ConfigureAwait(false);
                await channel.QueueDeclareAsync(payload.ChannelName, exclusive: false);

                var consumer = new AsyncEventingBasicConsumer(channel);

                switch (payload.ChannelType)
                {
                    case ChannelType.OcppEvent:
                        consumer.ReceivedAsync += RunEvent;
                        break;

                    default:
                        break;
                }

                _channels.Add(payload.ChannelName, new ChannelSemaphore(channel));
                await channel.BasicConsumeAsync(payload.ChannelName, true, consumer);
                break;

            case CommsFlag.Remove:
                if (_channels.ContainsKey(payload.ChannelName))
                {
                    var recordedChannel = await _channels[payload.ChannelName].GetChannelAsync();
                    await recordedChannel.CloseAsync();
                    await recordedChannel.DisposeAsync();

                    _channels.Remove(payload.ChannelName);
                }
                break;
        }
    }

    public async Task Run()
    {
        var channel = await _connection.CreateChannelAsync().ConfigureAwait(false);
        await channel.QueueDeclareAsync(_settings.SignalChannel, exclusive: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += ConsumeCommunicationSignal;

        await channel.BasicConsumeAsync(_settings.SignalChannel, true, consumer);

        await _runningTask.Task;
    }
}
