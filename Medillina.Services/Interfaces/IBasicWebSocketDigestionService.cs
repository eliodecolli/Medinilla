using System.Net.WebSockets;

namespace Medinilla.Services.Interfaces;

public interface IBasicWebSocketDigestionService
{
    Task Consume(WebSocket webSocket, string clientIdentifier);

    Task Send(string payload, string action);
}
