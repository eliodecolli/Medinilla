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

internal sealed class CoreInterfaceCommunication(
    IServiceProvider serviceProvider,
    IReceiver receiver,
    ISender sender,
    ILogger<CoreInterfaceCommunication> logger,
    CommunicationSettings settings)
    : IInterfaceCommunication
{

    public async Task Run(CancellationToken ct)
    {
        logger.LogInformation("Started core service...");
        await RunEvent(settings.RequestQueue, settings.ResponseQueue, ct);
    }

    private async Task RunEvent(string requestChannel, string responseChannelPrefix, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await receiver.ReceiveAsync(requestChannel, ct);
                if (result is null)
                {
                    continue;
                }

                logger.LogInformation("[{rc}]: {len} bytes", requestChannel, result.Length);

                var comms = Comms.Parser.ParseFrom(result);

                switch (comms.MessageType)
                {
                    case CommsMessageType.OcppRequest:
                    case CommsMessageType.OcppResponse:
                    {
                        var ocpp = OcppMessage.Parser.ParseFrom(comms.Payload);
                        _ = Task.Run(() => ProcessOcppAsync(ocpp.ClientIdentifier, ocpp.Payload.ToByteArray(), responseChannelPrefix));
                        break;
                    }

                    case CommsMessageType.ClientDisconnect:
                    {
                        var dc = ClientDisconnectMessage.Parser.ParseFrom(comms.Payload);
                        _ = Task.Run(() => DisconnectAsync(dc.ClientIdentifier));
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError("InterfaceComms: [{rc}]: Error: {msg}", requestChannel, ex.Message);
            }
        }
    }

    private async Task ProcessOcppAsync(string clientIdentifier, byte[] payload, string responseChannelPrefix)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var router = scope.ServiceProvider.GetRequiredService<IOcppCallRouter>();

            var result = await router.RouteOcppCall(payload, clientIdentifier);
            if (result is null)
            {
                return;
            }

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
            await sender.SendAsync(channel, response.ToByteArray());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OCPP call failed for {Client}", clientIdentifier);
        }
    }

    private async Task DisconnectAsync(string clientIdentifier)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var router = scope.ServiceProvider.GetRequiredService<IOcppCallRouter>();
            await router.DisconnectClient(clientIdentifier);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Disconnect failed for {Client}", clientIdentifier);
        }
    }
}
