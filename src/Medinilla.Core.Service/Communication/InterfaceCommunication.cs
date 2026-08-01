using Google.Protobuf;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Service.Interfaces;
using Medinilla.Core.Service.Types;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Core.SharedContracts.Comms.Ocpp;
using Medinilla.RealTime;
using Medinilla.RealTime.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Service.Communication;

internal class CoreInterfaceCommunication : IInterfaceCommunication
{
    private readonly ILogger<CoreInterfaceCommunication> _logger;
    private readonly IMessageQueue _inbound;
    private readonly IMessageQueue _outbound;
    private readonly IServiceProvider _serviceProvider;

    public CoreInterfaceCommunication(
        IServiceProvider serviceProvider,
        [FromKeyedServices("inbound")] IMessageQueue inbound,
        [FromKeyedServices("outbound")] IMessageQueue outbound,
        ILogger<CoreInterfaceCommunication> logger)
    {
        _serviceProvider = serviceProvider;
        _inbound = inbound;
        _outbound = outbound;
        _logger = logger;
    }

    public async Task Run(CommunicationSettings settings)
    {
        _logger.LogInformation("Started core service...");
        await RunEvent(settings.RequestQueue, settings.ResponseQueue);
    }

    private async Task RunEvent(string requestChannel, string responseChannelPrefix)
    {
        while (true)
        {
            var result = await _inbound.ReceiveAsync(requestChannel);
            if (result is null)
            {
                continue;
            }

            var comms = Comms.Parser.ParseFrom(result);

            switch (comms.MessageType)
            {
                case CommsMessageType.OcppRequest:
                    var ocpp = OcppMessage.Parser.ParseFrom(comms.Payload);
                    _ = Task.Run(() => ProcessOcppAsync(ocpp.ClientIdentifier, ocpp.Payload.ToByteArray(), responseChannelPrefix));
                    break;

                case CommsMessageType.OcppResponse:
                    break;

                case CommsMessageType.ClientDisconnect:
                    var dc = ClientDisconnectMessage.Parser.ParseFrom(comms.Payload);
                    _ = Task.Run(() => DisconnectAsync(dc.ClientIdentifier));
                    break;
            }
        }
    }

    private async Task ProcessOcppAsync(string clientIdentifier, byte[] payload, string responseChannelPrefix)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var router = scope.ServiceProvider.GetRequiredService<IOcppCallRouter>();

            var result = await router.RouteOcppCall(payload, clientIdentifier);

            var proto = new WampResult
            {
                ClientIdentifier = clientIdentifier,
                Result = result.Result?.ToByteArray() is { } r ? ByteString.CopyFrom(r) : ByteString.Empty,
                Error = result.Error?.ToByteArray() is { } e ? ByteString.CopyFrom(e) : ByteString.Empty,
                ReturnToCS = result.ReturnToCS,
            };

            var response = new Comms
            {
                MessageType = CommsMessageType.OcppResponse,
                Payload = proto.ToByteString(),
            };

            var channel = RedisUtils.BuildChannelName(responseChannelPrefix, clientIdentifier);
            await _outbound.SendAsync(response.ToByteArray(), channel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCPP call failed for {Client}", clientIdentifier);
        }
    }

    private async Task DisconnectAsync(string clientIdentifier)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var router = scope.ServiceProvider.GetRequiredService<IOcppCallRouter>();
            await router.DisconnectClient(clientIdentifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Disconnect failed for {Client}", clientIdentifier);
        }
    }
}
