using Akka.Actor;
using Akka.Hosting;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Service.Communication.Actors;
using Medinilla.Core.Service.Interfaces;
using Medinilla.Core.Service.Types;
using Medinilla.Core.SharedContracts.ActorPayloads;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Core.SharedContracts.Comms.Ocpp;
using Medinilla.RealTime.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Service.Communication;

internal class CoreInterfaceCommunication : IInterfaceCommunication
{
    private ILogger<CoreInterfaceCommunication> _logger;

    private IRequiredActor<Coordinator> _coordinator;
    private IActorRef _dispatcher;

    private readonly ActorSystem _system;
    private readonly IRedisQueue _queue;

    private readonly IServiceProvider _serviceProvider;

    public CoreInterfaceCommunication(IServiceProvider serviceProvider, [FromKeyedServices("inbound")] IRedisQueue redisQueue, ILogger<CoreInterfaceCommunication> logger, IOcppCallRouter router, IRequiredActor<Coordinator> coordinator, ActorSystem system)
    {
        _logger = logger;
        _coordinator = coordinator;
        _system = system;
        _queue = redisQueue;
        _serviceProvider = serviceProvider;
    }

    private async Task RunEvent(string channelName)
    {
        var result = await _queue.WaitForMessage(channelName);
        if (result is null)
        {
            return;
        }

        var comms = Comms.Parser.ParseFrom(result);

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

        Task.Run(() => RunEvent(channelName));
    }

    public async Task Run(CommunicationSettings settings)
    {
        _dispatcher = _system.ActorOf(Props.Create<Dispatcher>(_serviceProvider, settings.ResponseQueue), "ocpp-dispatcher");
        Task.Run(() => RunEvent(settings.RequestQueue));

        _logger.LogInformation("Started core service...");
    }
}
