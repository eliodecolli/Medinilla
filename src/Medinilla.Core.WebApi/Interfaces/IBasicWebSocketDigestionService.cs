using Medinilla.Infrastructure.Core;
using Medinilla.Infrastructure.WAMP;
using System.Net.WebSockets;

namespace Medinilla.WebApi.Interfaces;

public interface IBasicWebSocketDigestionService
{
    Task Consume(WebSocket webSocket, string clientIdentifier);
}
