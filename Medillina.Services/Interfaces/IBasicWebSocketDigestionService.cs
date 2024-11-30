using Medinilla.DataTypes.WAMP;
using Medinilla.Services.v1;
using System.Net.WebSockets;

namespace Medinilla.Services.Interfaces;

public interface IBasicWebSocketDigestionService
{
    Task Consume(WebSocket webSocket, string clientIdentifier);

    Task<WSDigestionServiceCallResult> Send(OcppCallRequest request);
}
