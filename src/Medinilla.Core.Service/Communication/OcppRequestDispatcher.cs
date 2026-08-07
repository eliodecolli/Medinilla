using Google.Protobuf;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Service.Exceptions;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.RealTime;

namespace Medinilla.Core.Service.Communication;

internal sealed class OcppRequestDispatcher(
    ISender sender,
    IWebSocketRoutingTable routing) : IOcppRequestDispatcher
{
    public async Task SubmitRequest(string clientIdentifier, byte[] payload)
    {
        // No retry, no fallback — a lookup miss means nobody can receive the CALL.
        var responseQueue = await routing.GetResponseQueueAsync(clientIdentifier)
            ?? throw new ChargerNotConnectedException(clientIdentifier);

        var comms = new Comms
        {
            MessageType = CommsMessageType.OcppRequest,
            ClientIdentifier = clientIdentifier,
            Payload = ByteString.CopyFrom(payload),
        };

        var queued = new QueuedMessageResponse
        {
            ClientIdentifier = clientIdentifier,
            Payload = comms,
        };

        await sender.SendAsync(responseQueue, queued.ToByteArray());
    }
}
