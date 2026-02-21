using System.Net.WebSockets;

namespace Medinilla.WebApi.Interfaces;

public interface IBasicWebSocketDigestionService : IAsyncDisposable
{
    Task Consume(WebSocket webSocket, string clientIdentifier);
}
