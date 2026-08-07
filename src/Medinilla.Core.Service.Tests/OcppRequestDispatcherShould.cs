using Medinilla.Core.Service.Communication;
using Medinilla.Core.Service.Exceptions;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.RealTime;
using Moq;
using System.Text;

namespace Medinilla.Core.Service.Tests;

public class OcppRequestDispatcherShould
{
    private const string CLIENT_ID = "TEST-CHARGER-001";
    private const string RESPONSE_QUEUE = "medinilla.ws.deadbeef.response";

    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<IWebSocketRoutingTable> _routingMock = new();

    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("[2,\"out-1\",\"Reset\",{}]");

    public OcppRequestDispatcherShould()
    {
        _senderMock
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private OcppRequestDispatcher CreateDispatcher()
        => new(_senderMock.Object, _routingMock.Object);

    private void GivenChargerIsHostedOn(string? queue)
        => _routingMock
            .Setup(r => r.GetResponseQueueAsync(CLIENT_ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queue);

    [Fact]
    public async Task SendToTheQueueTheRoutingTableReturns()
    {
        GivenChargerIsHostedOn(RESPONSE_QUEUE);

        await CreateDispatcher().SubmitRequest(CLIENT_ID, Payload);

        _senderMock.Verify(
            s => s.SendAsync(RESPONSE_QUEUE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenNoInstanceHostsTheCharger()
    {
        GivenChargerIsHostedOn(null);

        var ex = await Assert.ThrowsAsync<ChargerNotConnectedException>(
            () => CreateDispatcher().SubmitRequest(CLIENT_ID, Payload));

        Assert.Equal(CLIENT_ID, ex.ClientIdentifier);
    }

    // A lookup miss is a hard failure: nothing may be pushed anywhere.
    [Fact]
    public async Task NotSendAnythingWhenTheLookupMisses()
    {
        GivenChargerIsHostedOn(null);

        await Assert.ThrowsAsync<ChargerNotConnectedException>(
            () => CreateDispatcher().SubmitRequest(CLIENT_ID, Payload));

        _senderMock.Verify(
            s => s.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WrapThePayloadInAQueuedMessageResponse()
    {
        GivenChargerIsHostedOn(RESPONSE_QUEUE);

        byte[]? sent = null;
        _senderMock
            .Setup(s => s.SendAsync(RESPONSE_QUEUE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], CancellationToken>((_, bytes, _) => sent = bytes)
            .Returns(Task.CompletedTask);

        await CreateDispatcher().SubmitRequest(CLIENT_ID, Payload);

        var envelope = QueuedMessageResponse.Parser.ParseFrom(sent!);

        Assert.Equal(CLIENT_ID, envelope.ClientIdentifier);
        Assert.Equal(CLIENT_ID, envelope.Payload.ClientIdentifier);
        Assert.Equal(CommsMessageType.OcppRequest, envelope.Payload.MessageType);
        Assert.Equal(Payload, envelope.Payload.Payload.ToByteArray());
    }
}
