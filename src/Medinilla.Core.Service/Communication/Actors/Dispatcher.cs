using Akka.Actor;
using Google.Protobuf;
using Medinilla.Core.SharedContracts.ActorPayloads;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Core.SharedContracts.Comms.Ocpp;
using Medinilla.RealTime.Redis;
using Microsoft.Extensions.DependencyInjection;

namespace Medinilla.Core.Service.Communication.Actors;

public class Dispatcher : ReceiveActor
{
    private readonly IRedisQueue _queue;
    private readonly string _responseChannelPrefix;

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
        await _queue.SendMessage(response.ToByteArray(), responseChannel);
    }

    public Dispatcher(IServiceProvider serviceProvider, string responseChannelPrefix)
    {
        _responseChannelPrefix = responseChannelPrefix;
        _queue = serviceProvider.GetRequiredKeyedService<IRedisQueue>("outbound");

        ReceiveAsync<WampResultMessage>(DispatchResponse);
    }
}
