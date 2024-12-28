using Medinilla.DataTypes.Core;
using Medinilla.DataTypes.WAMP;
using Medinilla.Services.v1;
using System.Net.WebSockets;

namespace Medinilla.Services.Interfaces;

public interface IBasicWebSocketDigestionService : IDisposable
{
    Task Consume(WebSocket webSocket, string clientIdentifier);

    Task<WebSocketResponse> Send(OcppCallRequest request);
}
