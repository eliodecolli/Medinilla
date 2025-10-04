using Akka.Actor;
using Akka.DependencyInjection;
using Medinilla.Core.SharedContracts.ActorPayloads;

namespace Medinilla.Core.Service.Communication.Actors;

public sealed class Coordinator : ReceiveActor
{
    private DependencyResolver _resolver;

    private IActorRef GetOrCreateConsumer(string clientIdentifier)
    {
        var consumer = Context.Child(clientIdentifier);

        if (consumer.IsNobody())
        {
            consumer = Context.ActorOf(_resolver.Props<OcppConsumer>(), clientIdentifier);
        }
        return consumer;
    }

    private void RouteToConsumer(OcppConsumerMessage message)
    {
        var consumer = GetOrCreateConsumer(message.ClientIdentifier);
        consumer.Forward(message);
    }

    private void TerminateClient(ClientTerminateMessage message)
    {
        var consumer = Context.Child(message.ClientIdentifier);

        if (!consumer.IsNobody())
        {
            Context.Stop(consumer);
        }
    }

    public Coordinator(IServiceProvider sp, DependencyResolver resolver)
    {
        _resolver = resolver;

        Receive<OcppConsumerMessage>(RouteToConsumer);
        Receive<ClientTerminateMessage>(TerminateClient);
    }
}
