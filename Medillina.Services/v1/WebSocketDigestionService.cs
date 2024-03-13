using Medinilla.DataTypes.WAMP;
using Medinilla.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;

namespace Medinilla.Services.v1;

public class WebSocketDigestionService : IBasicWebSocketDigestionService
{
    private readonly IOcppCallRouter _callRouter;
    private readonly ILogger<WebSocketDigestionService> _logger;
    private readonly IWebSocketFactory _webSocketsFactory;

    private WebSocket _webSocket;
    private string _clientIndentifier;

    private string _currentMessageId;

    public WebSocketDigestionService(IWebSocketFactory webSocketsFactory, IOcppCallRouter callRouter, ILogger<WebSocketDigestionService> logger)
    {
        _callRouter = callRouter;
        _logger = logger;
        _webSocketsFactory = webSocketsFactory;
    }

    public async Task Send(string payload, string action)
    {
        if(_currentMessageId == null)
        {
            _currentMessageId = Guid.NewGuid().ToString();
            var request = new OcppCallRequest(_currentMessageId, action, payload);
            var buffer = request.ToByteArray();

            await _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            _logger.LogInformation("Sent {0} bytes to {1}", buffer.Length, _clientIndentifier);
        }
    }

    public async Task Consume(WebSocket webSocket, string clientIdentifier)
    {
        _webSocket = webSocket;
        _clientIndentifier = clientIdentifier;
        _webSocketsFactory.RegisterWebSocketDigestionService(clientIdentifier, this);
        
        while(true) {
            var buffer = new ArraySegment<byte>(new byte[1024]);
            var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
            if (result.CloseStatus.HasValue)
            {
                _logger.LogWarning("WS connection for {0} has been closed. {1}:{2}",
                    clientIdentifier, result.CloseStatus.Value, result.CloseStatusDescription);
                return;
            }

            if (result.Count > 0)
            {
                var received = buffer.Take(result.Count).ToArray();
                _logger.LogInformation($"Received {result.Count} bytes from {clientIdentifier}");

                var rpcResult = await _callRouter.RouteOcppCall(received, clientIdentifier);
                if(rpcResult.Error is not null)
                {
                    if (rpcResult.ReturnToCS)
                    {
                        await webSocket.SendAsync(rpcResult.Error.ToByteArray(), WebSocketMessageType.Text, true, CancellationToken.None)
                            .ConfigureAwait(false);

                        _logger.LogError("Error while handling message {0}: {1} - {2}",
                            rpcResult.Error.MessageId, rpcResult.Error.ErrorCode, rpcResult.Error.ErrorDescription);
                    }
                    else
                    {
                        _logger.LogInformation("Received error result for message {0}: {1} - {2}",
                            rpcResult.Error.MessageId, rpcResult.Error.ErrorCode, rpcResult.Error.ErrorDescription);
                    }
                }
                else if(rpcResult.Result is not null)
                {
                    if(rpcResult.ReturnToCS)
                    {
                        await webSocket.SendAsync(rpcResult.Result.ToByteArray(), WebSocketMessageType.Text, true, CancellationToken.None)
                            .ConfigureAwait(false);

                        _logger.LogInformation("Replied to {0} regarding {1}", clientIdentifier, rpcResult.Result.MessageId);
                    }
                    else
                    {
                        _logger.LogInformation("Received response for {0} from {1}", rpcResult.Result.MessageId, clientIdentifier);
                    }
                }
            }
            else
            {
                _logger.LogWarning("Received EMPTY data from ws.");
            }
        }
    }
}
