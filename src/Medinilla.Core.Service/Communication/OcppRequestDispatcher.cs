using Google.Protobuf;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Service.Types;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.RealTime;
using Medinilla.RealTime.Redis;

namespace Medinilla.Core.Service.Communication;

internal sealed class OcppRequestDispatcher(ISender sender, CommunicationSettings settings) : IOcppRequestDispatcher
{
    public async Task SubmitRequest(string clientIdentifier, byte[] payload)
    {
        var comms = new Comms
        {
            MessageType = CommsMessageType.OcppRequest,
            ClientIdentifier = clientIdentifier,
            Payload = ByteString.CopyFrom(payload),
        };

        var channelName = RedisUtils.BuildChannelName(settings.ResponseQueue, clientIdentifier);
        await sender.SendAsync(channelName, comms.ToByteArray());
    }
}
