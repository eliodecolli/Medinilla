using Akka.Actor;
using Akka.Hosting;
using Medinilla.Core.Service.Communication.Actors;
using Medinilla.Core.Service.Interfaces;
using Medinilla.Core.Service.Types;
using Medinilla.Core.SharedContracts.ActorPayloads;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Core.SharedContracts.Comms.Ocpp;
using Medinilla.RealTime;
using Medinilla.Core.Interfaces;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

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

    public CoreInterfaceCommunication(ICommunicationProvider commsProvider, ILogger<CoreInterfaceCommunication> logger, IOcppCallRouter router, IRequiredActor<Coordinator> coordinator, ActorSystem system)
    {
        _logger = logger;

        _coordinator = coordinator;
        _system = system;

        _comms = commsProvider.GetMessenger("RabbitMQ") ?? throw new InvalidOperationException("Could not load RabbitMQ messenger interface");
    }

    private Task RunEvent(object state)
    {
        if (state is not Dictionary<string, object> args)
        {
            return Task.CompletedTask;
        }

        if (args["ea"] is not BasicDeliverEventArgs ea)
        {
            return Task.CompletedTask;
        }
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

        return Task.CompletedTask;
    }

    public async Task Run(CommunicationSettings settings)
    {
        // set up request channel
        await _comms.RegisterChannel(settings.RequestQueue);
        await _comms.RegisterHandler(settings.RequestQueue, RunEvent);

        // set up response channel
        await _comms.RegisterChannel(settings.ResponseQueue);
        _responseChannel = (IChannel)_comms.GetChannel(settings.ResponseQueue);

        _dispatcher = _system.ActorOf(Props.Create<Dispatcher>(_responseChannel, settings.ResponseQueue), "ocpp-dispatcher");

        _logger.LogDebug("Started core service...");
    }
}
