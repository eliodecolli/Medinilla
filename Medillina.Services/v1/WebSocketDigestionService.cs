using Medinilla.DataTypes.Core;
using Medinilla.DataTypes.WAMP;
using Medinilla.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace Medinilla.Services.v1;

public class WebSocketDigestionService(IServiceProvider serviceProvider, ILogger<WebSocketDigestionService> _logger) : IBasicWebSocketDigestionService
{
    private WebSocket _webSocket;
    private string _clientIdentifier;

    private ConcurrentQueue<(OcppCallRequest, TaskCompletionSource<WebSocketResponse>)> _queuedRequests = new();
    private object _lock = new();
    private object _lockQueue = new();

    private string _currentCall;
    private bool queueProcessRunning;

    private void SetCurrentCall(string value)
    {
        lock(_lock)
        {
            _currentCall = value;
        }
    }

    private bool IsServiceBusy()
    {
        lock (_lock)
        {
            return !string.IsNullOrEmpty(_currentCall);
        }
    }

    private async Task<WebSocketResponse> DoSend(OcppCallRequest request)
    {
        try
        {
            SetCurrentCall(request.MessageId);
            var data = request.ToBytes();

            await _webSocket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None)
                .ConfigureAwait(false);

            var buffer = await AwaitWebSocketResponse();
            if (buffer is not null)
            {
                var parser = new OcppMessageParser();

                var messageTextBlob = Encoding.UTF8.GetString(buffer);
                parser.LoadRaw(messageTextBlob);

                switch(parser.GetMessageType())
                {
                    case OcppJMessageType.CALL_RESULT:
                        return new WebSocketResponse()
                        {
                            OcppCallResult = parser.ParseResult(),
                            ResponseStatus = WebSocketResponseStatus.Success,
                        };
                    case OcppJMessageType.CALL_ERROR:
                        return new WebSocketResponse()
                        {
                            OcppCallError = parser.ParseError(),
                            ResponseStatus = WebSocketResponseStatus.Success,
                        };
                }
            }

            return new WebSocketResponse()
            {
                ResponseStatus = WebSocketResponseStatus.Fail,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("Unable to send OCPP Call Request. Exception: " + ex);
            return new WebSocketResponse()
            {
                ResponseStatus = WebSocketResponseStatus.Fail,
            };
        }
    }

    private void StartQueueProcessor()
    {
        lock (_lockQueue)
        {
            if (queueProcessRunning)
            {
                return;
            }

            queueProcessRunning = true;
        }

        try
        {
            Task.Run(async () =>
            {
                while (_queuedRequests.TryDequeue(out var dequeuedRequest))
                {
                    var (request, tcs) = dequeuedRequest;

                    try
                    {
                        var response = await DoSend(request).ConfigureAwait(false);
                        tcs.SetResult(response);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }
            });
        }
        finally
        {
            lock (_lockQueue)
            {
                queueProcessRunning = false;
            }
        }
    }

    private async Task<byte[]?> AwaitWebSocketResponse()
    {
        var buffer = new ArraySegment<byte>(new byte[1024]);
        var result = await _webSocket.ReceiveAsync(buffer, CancellationToken.None);
        if (result.CloseStatus.HasValue)
        {
            _logger.LogWarning("WS connection for {0} has been closed. {1}:{2}",
                _clientIdentifier, result.CloseStatus.Value, result.CloseStatusDescription);
            return null;
        }

        return buffer.Take(result.Count).ToArray();
    }

    public async Task<WebSocketResponse> Send(OcppCallRequest request)
    {
        if (!IsServiceBusy())
        {
            return await DoSend(request).ConfigureAwait(false);
        }
        else
        {
            _logger.LogWarning("Trying to send a message to {0} but there's a pending request waiting for a response.", _clientIdentifier);
            var tsc = new TaskCompletionSource<WebSocketResponse>();
            _queuedRequests.Enqueue(new (request, tsc));

            StartQueueProcessor();

            return await tsc.Task.ConfigureAwait(false);
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
        _clientIdentifier = clientIdentifier;
        
        while(true) {
           if(!IsServiceBusy())
            {
                var received = await AwaitWebSocketResponse().ConfigureAwait(false);

                if (received is not null)
                {
                    var callRouter = GetCallRouter();
                    var rpcResult = await callRouter!.RouteOcppCall(received, clientIdentifier);

                    if (rpcResult.Error is not null)
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
                    else if (rpcResult.Result is not null)
                    {
                        if (_currentCall != rpcResult.Result.MessageId)
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
}
