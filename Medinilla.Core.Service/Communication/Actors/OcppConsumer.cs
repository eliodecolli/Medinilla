using Akka.Actor;
using Medinilla.Core.SharedContracts.ActorPayloads;
using Medinilla.Infrastructure.WAMP;
using Medinilla.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Service.Communication.Actors;

public sealed class OcppConsumer : ReceiveActor
{
    private IOcppCallRouter _router;
    private ILogger<OcppConsumer> _logger;

    private IServiceScope _scope;

    private async Task ConsumeOcppCall(OcppConsumerMessage message)
    {
        _logger.LogInformation($"Received OCPP Message from: {message.ClientIdentifier}");
        var result = await _router.RouteOcppCall(message.Payload, message.ClientIdentifier);
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
        _scope = sp.CreateScope();
        _router = _scope.ServiceProvider.GetRequiredService<IOcppCallRouter>();
        _logger = _scope.ServiceProvider.GetRequiredService<ILogger<OcppConsumer>>();

        ReceiveAsync<OcppConsumerMessage>(ConsumeOcppCall);  // remember: this message is being forwarded to us
    }

    protected override void PostStop()
    {
        _scope.Dispose();
        base.PostStop();
    }
}
