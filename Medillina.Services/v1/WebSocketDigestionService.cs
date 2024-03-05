using Medinilla.DataTypes.WAMP;
using Medinilla.Services.Interfaces;
using System.Net.WebSockets;
using System.Text;

namespace Medinilla.Services.v1;

public class WebSocketDigestionService : IBasicWebSocketDigestionService
{
    private readonly IOcppCallRouter _callRouter;

    public WebSocketDigestionService(IOcppCallRouter callRouter)
    {
        _callRouter = callRouter;
    }

    public async Task Consume(WebSocket webSocket, string clientIdentifier)
    {
        var buffer = new ArraySegment<byte>(new byte[1024]);
        
        while(true) {
            var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
            if (result.CloseStatus.HasValue)
            {
                Console.WriteLine("WS connection has been closed. {0}:{1}", result.CloseStatus.Value, result.CloseStatusDescription);
                return;
            }

            if (result.Count > 0)
            {
                var received = buffer.Take(result.Count).ToArray();
                Console.WriteLine($"Received {result.Count} bytes from {clientIdentifier}");

                await _callRouter.RouteOcppCall(received, clientIdentifier);
            }
            else
            {
                Console.WriteLine("Received EMPTY data from ws.");
            }
        }
    }
}
