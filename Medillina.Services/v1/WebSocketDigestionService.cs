using Google.Protobuf;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Core.SharedContracts.Comms.Ocpp;
using Medinilla.DataTypes.Core;
using Medinilla.DataTypes.WAMP;
using Medinilla.Infrastructure.Interops;
using Medinilla.RealTime;
using Medinilla.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace Medinilla.Services.v1;

public class WebSocketDigestionService(IServiceProvider serviceProvider,
    IConfiguration config,
    ILogger<WebSocketDigestionService> _logger,
    ICommunicationProvider commsProvider) : IBasicWebSocketDigestionService
{
    private WebSocket _webSocket;
    private string _clientIdentifier;

    private ConcurrentQueue<(OcppCallRequest, TaskCompletionSource<WebSocketResponse>)> _queuedRequests = new();
    private object _lock = new();
    private object _lockQueue = new();

    private string _currentCall;
    private bool queueProcessRunning;

    public void Dispose()
    {
        _queuedRequests.Clear();
        _webSocket.Dispose();
    }

    #region Private Calls
    private void SetCurrentCall(string value)
    {
        lock (_lock)
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

    private bool CanRun()
    {
        return !IsServiceBusy() && _webSocket.State == WebSocketState.Open;
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

                switch (parser.GetMessageType())
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
        if (_webSocket.State == WebSocketState.Open)
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
        else
        {
            _logger.LogWarning($"WS connection has been updated. New status: {Enum.GetName(_webSocket.State)}.");
            return null;
        }
    }
    #endregion

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
            _queuedRequests.Enqueue(new(request, tsc));

            StartQueueProcessor();

            return await tsc.Task.ConfigureAwait(false);
        }
    }

    private async Task RunCommsChannel(object state)
    {
        var args = (Dictionary<string, object>)state;
        var ea = (BasicDeliverEventArgs)args["ea"];

        if (_webSocket.State != WebSocketState.Open)
        {
            _logger.LogWarning($"Trying to consume response from channel {ea.RoutingKey} but the underlying websocket is not available.");
            var channel = (args["model"] as AsyncEventingBasicConsumer)!.Channel;  // eww

            await channel.BasicRejectAsync(ea.DeliveryTag, true).ConfigureAwait(false);
            return;
        }

        try
        {
            var result = ea.Body.ToArray();

            var commsResult = Comms.Parser.ParseFrom(result);
            switch (commsResult.MessageType)
            {
                case CommsMessageType.OcppResponse:
                    var parsed = WampResult.Parser.ParseFrom(commsResult.Payload.ToByteArray());
                    if (!parsed.Result.IsEmpty)
                    {
                        await _webSocket.SendAsync(parsed.Result.ToArray(), WebSocketMessageType.Text, true, CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                    else if (!parsed.Error.IsEmpty)
                    {
                        await _webSocket.SendAsync(parsed.Error.ToArray(), WebSocketMessageType.Text, true, CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                    break;
                case CommsMessageType.OcppRequest:
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error from {ea.RoutingKey}: {ex.ToString()}");

        }
    }

    public async Task Consume(WebSocket webSocket, string clientIdentifier)
    {
        _webSocket = webSocket;
        _clientIdentifier = clientIdentifier;

        var comms = commsProvider.GetMessenger("RabbitMQ");

        if (comms is null)
        {
            _logger.LogError("Unable to get a messenger for RabbitMQ.");
            throw new Exception("Unable to get a messenger for RabbitMQ.");
        }

        var signalChannelName = config.GetSection("Comms")["SignalChannel"];
        if (string.IsNullOrEmpty(signalChannelName))
        {
            throw new Exception("Signal channel name is not set.");
        }

        var channelName = CommsUtils.GetChannelName(clientIdentifier);

        await comms.RegisterChannel(signalChannelName);

        await comms.RegisterChannel(channelName);

        // handle response flow
        var responseChannelName = CommsUtils.GetResponseChannelName(channelName);
        await comms.RegisterChannel(responseChannelName);
        await comms.RegisterHandler(responseChannelName, RunCommsChannel);

        await comms.SendMessage(signalChannelName, new CommunicationChannelSignal()
        {
            ChannelName = channelName,
            ChannelType = ChannelType.OcppEvent,
        }.ToByteArray());

        _logger.LogInformation($"Signaled client for {clientIdentifier} to start listening on {channelName} with response on {responseChannelName}.");

        while (true)
        {
            if (!IsServiceBusy() && _webSocket.State == WebSocketState.Open)
            {
                var received = await AwaitWebSocketResponse().ConfigureAwait(false);

                if (received is not null)
                {
                    _logger.LogInformation($"Received {received.Length} bytes from {clientIdentifier}");

                    var payload = new OcppMessage()
                    {
                        ClientIdentifier = clientIdentifier,
                        Payload = ByteString.CopyFrom(received),
                    };

                    await comms.SendMessage(channelName, payload.ToByteArray()).ConfigureAwait(false);
                }
                else
                {
                    _logger.LogWarning("Received EMPTY data from ws.");
                }
            }
            else if (webSocket.CloseStatus.HasValue)
            {
                _logger.LogWarning("WS connection for {0} has been closed. {1}:{2}",
                    _clientIdentifier, webSocket.CloseStatus.Value, webSocket.CloseStatusDescription);

                await comms.SendMessage(signalChannelName, new CommunicationChannelSignal()
                {
                    ChannelName = channelName,
                    ChannelType = ChannelType.Other,
                    Flag = CommsFlag.Remove,
                }.ToByteArray());

                await comms.DestroyChannel(signalChannelName);

                break;
            }
        }
    }
}
