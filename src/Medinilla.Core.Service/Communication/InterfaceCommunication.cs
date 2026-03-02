using Akka.Actor;
using Akka.Hosting;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Service.Communication.Actors;
using Medinilla.Core.Service.Interfaces;
using Medinilla.Core.Service.Types;
using Medinilla.Core.SharedContracts.ActorPayloads;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Core.SharedContracts.Comms.Ocpp;
using Medinilla.RealTime;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Medinilla.Core.Service.Communication;

internal class CoreInterfaceCommunication : IInterfaceCommunication
{
    private CommunicationSettings _settings;
    private ILogger<CoreInterfaceCommunication> _logger;

    private IRequiredActor<Coordinator> _coordinator;
    private IActorRef _dispatcher;

    private readonly ActorSystem _system;

    private IChannel _responseChannel;

    private IRealTimeMessenger _comms;

    public CoreInterfaceCommunication(ICommunicationProvider provider, ILogger<CoreInterfaceCommunication> logger, IOcppCallRouter router, IRequiredActor<Coordinator> coordinator, ActorSystem system)
    {
        _logger = logger;

        _coordinator = coordinator;
        _system = system;

        _comms = provider.GetMessenger("Redis") ?? throw new Exception("No Redis messenger found");  // TODO: Use custom exceptions
    }

    private Task RunEvent(object state)
    {
        var comms = Comms.Parser.ParseFrom((byte[])state);

        switch (comms.MessageType)
        {
            case CommsMessageType.OcppRequest:
                var message = OcppMessage.Parser.ParseFrom(comms.Payload);
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

        return Task.CompletedTask;
    }

    public async Task Run(CommunicationSettings settings)
    {
        await _comms.RegisterChannel(settings.RequestQueue);
        await _comms.RegisterHandler(settings.RequestQueue, RunEvent);

        _dispatcher = _system.ActorOf(Props.Create<Dispatcher>(_comms, settings.ResponseQueue), "ocpp-dispatcher");

        _logger.LogDebug("Started core service...");
    }
}
