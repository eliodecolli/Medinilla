using Akka.Actor;
using Google.Protobuf;
using Medinilla.Core.SharedContracts.ActorPayloads;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Core.SharedContracts.Comms.Ocpp;
using RabbitMQ.Client;

namespace Medinilla.Core.Service.Communication.Actors;

public class Dispatcher : ReceiveActor
{
    private IChannel _channel;
    private string _responseChannel;

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

        await _channel.BasicPublishAsync("", _responseChannel, response.ToByteArray());
    }

    public Dispatcher(IChannel channel, string responseChannel)
    {
        _channel = channel;
        _responseChannel = responseChannel;

        ReceiveAsync<WampResultMessage>(DispatchResponse);
    }
}
