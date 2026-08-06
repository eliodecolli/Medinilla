using Google.Protobuf;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Service.Interfaces;
using Medinilla.Core.Service.Types;
using Medinilla.Core.SharedContracts.Comms;
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
                        var ocppBytes = comms.Payload.ToByteArray();
                        _ = Task.Run(() => ProcessOcppAsync(comms.ClientIdentifier, ocppBytes, responseChannelPrefix));
                        break;
                    }

                    case CommsMessageType.ClientDisconnect:
                    {
                        _ = Task.Run(() => DisconnectAsync(comms.ClientIdentifier));
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

            var responseBytes = result?.Error?.ToByteArray()
                ?? result?.Result?.ToByteArray();

            if (responseBytes is null)
            {
                return;
            }

            var response = new Comms
            {
                MessageType = CommsMessageType.OcppResponse,
                ClientIdentifier = clientIdentifier,
                Payload = ByteString.CopyFrom(responseBytes),
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
