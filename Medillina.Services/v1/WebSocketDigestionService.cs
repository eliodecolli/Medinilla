using Medinilla.DataTypes.WAMP;
using Medinilla.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;

namespace Medinilla.Services.v1;

public class WebSocketDigestionService(IServiceProvider serviceProvider, ILogger<WebSocketDigestionService> _logger) : IBasicWebSocketDigestionService
{
    private WebSocket _webSocket;
    private string _clientIndentifier;

    private string _currentCall;

    private void SetCurrentCall(string value)
    {
        _currentCall = value;
    }

    private bool IsServiceBusy()
    {
        return !string.IsNullOrEmpty(_currentCall);
    }

    public async Task Send(OcppCallRequest request)
    {
        if(!IsServiceBusy())
        {
            var data = request.ToBytes();

            await _webSocket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None)
                .ConfigureAwait(false);
            _logger.LogInformation("Sent {0} bytes to {1}", data.Length, _clientIndentifier);

            SetCurrentCall(request.MessageId);
        }
        else
        {
            _logger.LogWarning("Trying to send a message to {0} but there's a pending request waiting for a response.", _clientIndentifier);
        }
    }

    private IOcppCallRouter GetCallRouter()
    {
        // we need to make sure that we initialize a new router for every message, to avoid triggering the DbContext :(
        var scope = serviceProvider.CreateScope();
        return scope.ServiceProvider.GetService<IOcppCallRouter>()!;
    }


    public async Task Consume(WebSocket webSocket, string clientIdentifier)
    {
        _webSocket = webSocket;
        _clientIndentifier = clientIdentifier;
        
        while(true) {
            var buffer = new ArraySegment<byte>(new byte[1024]);
            var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.CloseStatus.HasValue)
            {
                _logger.LogWarning("WS connection for {0} has been closed. {1}:{2}",
                    clientIdentifier, result.CloseStatus.Value, result.CloseStatusDescription);
                return;
            }

            if (result.Count > 0)
            {
                var received = buffer.Take(result.Count).ToArray();

                var callRouter = GetCallRouter();
                var rpcResult = await callRouter!.RouteOcppCall(received, clientIdentifier);

                if(rpcResult.Error is not null)
                {
                    if (_currentCall != rpcResult.Error.MessageId)
                    {
                        await webSocket.SendAsync(rpcResult.Error.ToByteArray(), WebSocketMessageType.Text, true, CancellationToken.None);

                        _logger.LogError("Error while handling message {0}: {1} - {2}",
                            rpcResult.Error.MessageId, rpcResult.Error.ErrorCode, rpcResult.Error.ErrorDescription);
                    }
                    else
                    {
                        SetCurrentCall(string.Empty);
                        _logger.LogInformation("Received error result for message {0}: {1} - {2}",
                            rpcResult.Error.MessageId, rpcResult.Error.ErrorCode, rpcResult.Error.ErrorDescription);
                    }
                }
                else if(rpcResult.Result is not null)
                {
                    if(_currentCall != rpcResult.Result.MessageId)
                    {
                        await webSocket.SendAsync(rpcResult.Result.ToByteArray(), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    else
                    {
                        SetCurrentCall(string.Empty);
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
