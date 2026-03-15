using Akka.Actor;
using Medinilla.Core.SharedContracts.ActorPayloads;
using Medinilla.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Service.Communication.Actors;

public sealed class OcppConsumer : ReceiveActor
{
    private IServiceProvider _serviceProvider;

    private async Task ConsumeOcppCall(OcppConsumerMessage message)
    {
        // setup context - each new message should run under its own scope, so that we get a new DbContext, and Action for each one
        using var scope = _serviceProvider.CreateScope();
        var router = scope.ServiceProvider.GetRequiredService<IOcppCallRouter>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OcppConsumer>>();

        var result = await router.RouteOcppCall(message.Payload, message.ClientIdentifier);
        var messageResult = new WampResultMessage()
        {
            ClientIdentifier = message.ClientIdentifier,
            Result = result.Result?.ToByteArray(),
            Error = result.Error?.ToByteArray(),
            ReturnToCS = result.ReturnToCS,
        };

        Sender.Tell(messageResult, Self);
    }

    public OcppConsumer(IServiceProvider sp)
    {
        _serviceProvider = sp;

        ReceiveAsync<OcppConsumerMessage>(ConsumeOcppCall);  // remember: this message is being forwarded to us
    }


    protected override void PostStop()
    {
        using var scope = _serviceProvider.CreateScope();
        var router = scope.ServiceProvider.GetRequiredService<IOcppCallRouter>();

        router.DisconnectClient(Context.Self.Path.Name).RunSynchronously();   // not sure about the implications of this
        base.PostStop();
    }
}
