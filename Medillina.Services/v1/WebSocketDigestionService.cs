using Medinilla.Services.Interfaces;
using System.Net.WebSockets;
using System.Text;

namespace Medinilla.Services.v1;

public class WebSocketDigestionService : IBasicWebSocketDigestionService
{
    public async Task Consume(WebSocket webSocket)
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
                var received = buffer.Take(result.Count);
                var command = Encoding.UTF8.GetString(received.ToArray());

                if (command.ToLower() == "bye")
                {
                    Console.WriteLine("Received BYE command from ws.");
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);
                    return;
                }
                else
                {
                    Console.WriteLine("Received '{0}' form ws.", command);
                    await webSocket.SendAsync(Encoding.UTF8.GetBytes("Gotcha"), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            else
            {
                Console.WriteLine("Received EMPTY data from ws.");
            }
        }
    }
}
