using Akka.Actor;
using Google.Protobuf;
using Medinilla.Core.SharedContracts.ActorPayloads;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Core.SharedContracts.Comms.Ocpp;
using Medinilla.RealTime;
using Medinilla.RealTime.Redis;

namespace Medinilla.Core.Service.Communication.Actors;

public class Dispatcher : ReceiveActor
{
    private IRealTimeMessenger _comms;
    private string _responseChannelPrefix;

    private async Task DispatchResponse(WampResultMessage result)
    {
        var protoWampResult = new WampResult()
        {
            ClientIdentifier = result.ClientIdentifier,
            Result = result.Result is not null ? ByteString.CopyFrom(result.Result) : ByteString.Empty,
            Error = result.Error is not null ? ByteString.CopyFrom(result.Error) : ByteString.Empty,
            ReturnToCS = result.ReturnToCS,
        };

        var response = new Comms()
        {
            MessageType = CommsMessageType.OcppResponse,
            Payload = protoWampResult.ToByteString(),
        };

        var responseChannel = RedisUtils.BuildChannelName(_responseChannelPrefix, result.ClientIdentifier);
        await _comms.SendMessage(responseChannel, response.ToByteArray());
    }

    public Dispatcher(IRealTimeMessenger comms, string responseChannelPrefix)
    {
        _comms = comms;
        _responseChannelPrefix = responseChannelPrefix;

        ReceiveAsync<WampResultMessage>(DispatchResponse);
    }
}
