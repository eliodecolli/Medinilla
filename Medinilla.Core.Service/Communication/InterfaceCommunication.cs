using Akka.Actor;
using Akka.Hosting;
using Medinilla.Core.Service.Communication.Actors;
using Medinilla.Core.Service.Interfaces;
using Medinilla.Core.Service.Types;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Core.SharedContracts.Comms.Ocpp;
using Medinilla.Core.SharedContracts.ActorPayloads;
using Medinilla.Services.Interfaces;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Medinilla.Core.Service.Communication;

internal class CoreInterfaceCommunication : IInterfaceCommunication
{
    private CommunicationSettings _settings;
    private ConnectionFactory _connectionFactory;
    private IConnection _connection;
    private ILogger<CoreInterfaceCommunication> _logger;

    private IRequiredActor<Coordinator> _coordinator;
    private IActorRef _dispatcher;

    private readonly ActorSystem _system;

    private IChannel _responseChannel;

    public CoreInterfaceCommunication(ILogger<CoreInterfaceCommunication> logger, IOcppCallRouter router, IRequiredActor<Coordinator> coordinator, ActorSystem system)
    {
        _connectionFactory = new ConnectionFactory();
        _logger = logger;

        _coordinator = coordinator;
        _system = system;
    }

    private async Task Connect(CommunicationSettings settings)
    {
        _settings = settings;
        _connectionFactory.Uri = new Uri(settings.RabbitUri);
        _connection = await _connectionFactory.CreateConnectionAsync().ConfigureAwait(false);
    }

    private async Task RunEvent(object model, BasicDeliverEventArgs ea)
    {
        var comms = Comms.Parser.ParseFrom(ea.Body.ToArray());

        switch (comms.MessageType)
        {
            case CommsMessageType.OcppRequest:
                var message = OcppMessage.Parser.ParseFrom(ea.Body.ToArray());
                _coordinator.ActorRef.Tell(new OcppConsumerMessage()
                {
                    ClientIdentifier = message.ClientIdentifier,
                    Payload = message.Payload.ToByteArray()
                }, _dispatcher);
                break;

            case CommsMessageType.OcppResponse:
            // implement
            default:
                break;
        }
    }

    public async Task Run(CommunicationSettings settings)
    {
        await Connect(settings);

        var channel = await _connection.CreateChannelAsync().ConfigureAwait(false);
        await channel.QueueDeclareAsync(_settings.RequestQueue, exclusive: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += RunEvent;

        await channel.BasicConsumeAsync(_settings.RequestQueue, true, consumer);

        _responseChannel = await _connection.CreateChannelAsync().ConfigureAwait(false);
        await _responseChannel.QueueDeclareAsync(settings.ResponseQueue);

        _dispatcher = _system.ActorOf(Props.Create<Dispatcher>(_responseChannel, settings.ResponseQueue), "ocpp-dispatcher");

        _logger.LogDebug("Started core service...");
    }
}
