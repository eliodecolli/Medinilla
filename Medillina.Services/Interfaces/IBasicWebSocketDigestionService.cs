using Medinilla.DataTypes.WAMP;
using System.Net.WebSockets;

namespace Medinilla.Services.Interfaces;

public interface IBasicWebSocketDigestionService
{
    Task Consume(WebSocket webSocket, string clientIdentifier);

    Task Send(OcppCallRequest request);
}
