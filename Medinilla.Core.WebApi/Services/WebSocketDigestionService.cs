using Google.Protobuf;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Core.SharedContracts.Comms.Ocpp;
using Medinilla.Infrastructure;
using Medinilla.Infrastructure.Core;
using Medinilla.Infrastructure.Interops;
using Medinilla.Infrastructure.WAMP;
using Medinilla.RealTime;
using Medinilla.WebApi.Interfaces;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace Medinilla.WebApi.Services;

public class WebSocketDigestionService : IBasicWebSocketDigestionService, IAsyncDisposable, IDisposable
{
    private readonly IConfiguration _config;
    private readonly ILogger<WebSocketDigestionService> _logger;
    private readonly ICommunicationProvider _commsProvider;
    private readonly CancellationTokenSource _cts;

    private WebSocket? _webSocket;
    private string? _clientIdentifier;
    private IRealTimeMessenger? _commsMessenger;

    private readonly ConcurrentQueue<(OcppCallRequest, TaskCompletionSource<WebSocketResponse>)> _queuedRequests;
    private readonly object _lock;
    private readonly object _lockQueue;

    private string? _currentCall;
    private bool _queueProcessRunning;
    private bool _disposed;

    public WebSocketDigestionService(
        IConfiguration config,
        ILogger<WebSocketDigestionService> logger,
        ICommunicationProvider commsProvider)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _commsProvider = commsProvider ?? throw new ArgumentNullException(nameof(commsProvider));

        _queuedRequests = new ConcurrentQueue<(OcppCallRequest, TaskCompletionSource<WebSocketResponse>)>();
        _lock = new object();
        _lockQueue = new object();
        _cts = new CancellationTokenSource();

        _commsMessenger = _commsProvider.GetMessenger("RabbitMQ");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _cts.Cancel();

            if (_webSocket != null)
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Service disposing",
                        CancellationToken.None);
                }
                _webSocket.Dispose();
            }

            await CleanupCommunicationChannels();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during service disposal");
        }
        finally
        {
            _queuedRequests.Clear();
            _cts.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DisposeAsync().AsTask().GetAwaiter().GetResult();

        GC.SuppressFinalize(this);
    }

    private async Task CleanupCommunicationChannels()
    {
        if (_commsMessenger != null && _clientIdentifier != null)
        {
            try
            {
                var channelName = CommsUtils.GetChannelName(_clientIdentifier);
                var responseChannelName = CommsUtils.GetResponseChannelName(channelName);
                var signalChannelName = _config.GetSection("Comms")["SignalChannel"];

                if (!string.IsNullOrEmpty(signalChannelName))
                {
                    await _commsMessenger.SendMessage(signalChannelName, new CommunicationChannelSignal
                    {
                        ChannelName = channelName,
                        ChannelType = ChannelType.Other,
                        Flag = CommsFlag.Remove,
                    }.ToByteArray());

                    await _commsMessenger.DestroyChannel(channelName);
                    await _commsMessenger.DestroyChannel(responseChannelName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up communication channels");
            }
        }
    }

    private void SetCurrentCall(string? value)
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

    private bool IsConnectionOpen()
    {
        return _webSocket?.State == WebSocketState.Open;
    }

    private async Task RawSend(byte[] data)
    {
        if (_webSocket == null || !IsConnectionOpen())
        {
            throw new InvalidOperationException("WebSocket is not connected");
        }

        await _webSocket.SendAsync(
            data,
            WebSocketMessageType.Text,
            true,
            _cts.Token);
    }

    private async Task<WebSocketResponse> DoSend(OcppCallRequest request)
    {
        try
        {
            SetCurrentCall(request.MessageId);
            var data = request.ToBytes();

            await RawSend(data);

            var buffer = await AwaitWebSocketResponse();
            if (buffer != null)
            {
                var parser = new OcppMessageParser();
                var messageTextBlob = Encoding.UTF8.GetString(buffer);
                parser.LoadRaw(messageTextBlob);

                return parser.GetMessageType() switch
                {
                    OcppJMessageType.CALL_RESULT => new WebSocketResponse
                    {
                        OcppCallResult = parser.ParseResult(),
                        ResponseStatus = WebSocketResponseStatus.Success,
                    },
                    OcppJMessageType.CALL_ERROR => new WebSocketResponse
                    {
                        OcppCallError = parser.ParseError(),
                        ResponseStatus = WebSocketResponseStatus.Success,
                    },
                    _ => new WebSocketResponse
                    {
                        ResponseStatus = WebSocketResponseStatus.Fail,
                    }
                };
            }

            return new WebSocketResponse
            {
                ResponseStatus = WebSocketResponseStatus.Fail,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to send OCPP Call Request");
            return new WebSocketResponse
            {
                ResponseStatus = WebSocketResponseStatus.Fail,
            };
        }
        finally
        {
            SetCurrentCall(null);
        }
    }

    private void StartQueueProcessor()
    {
        lock (_lockQueue)
        {
            if (_queueProcessRunning)
            {
                return;
            }

            _queueProcessRunning = true;
        }

        Task.Run(async () =>
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested &&
                       _queuedRequests.TryDequeue(out var dequeuedRequest))
                {
                    var (request, tcs) = dequeuedRequest;

                    try
                    {
                        var response = await DoSend(request);
                        tcs.TrySetResult(response);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }
            }
            finally
            {
                lock (_lockQueue)
                {
                    _queueProcessRunning = false;
                }
            }
        }, _cts.Token);
    }

    private async Task<byte[]?> AwaitWebSocketResponse()
    {
        if (_webSocket == null || !IsConnectionOpen())
        {
            return null;
        }

        var buffer = new byte[1024];
        var segment = new ArraySegment<byte>(buffer);

        try
        {
            var result = await _webSocket.ReceiveAsync(segment, _cts.Token);

            if (result.CloseStatus.HasValue)
            {
                _logger.LogWarning("WS connection for {ClientId} has been closed. {Status}:{Description}",
                    _clientIdentifier, result.CloseStatus.Value, result.CloseStatusDescription);
                return null;
            }

            return segment.AsSpan(0, result.Count).ToArray();
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public async Task<WebSocketResponse> Send(OcppCallRequest request)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WebSocketDigestionService));
        }

        if (!IsServiceBusy())
        {
            return await DoSend(request);
        }

        _logger.LogWarning(
            "Queuing message for {ClientId} due to pending request",
            _clientIdentifier);

        var tcs = new TaskCompletionSource<WebSocketResponse>();
        _queuedRequests.Enqueue((request, tcs));
        StartQueueProcessor();

        return await tcs.Task;
    }

    private async Task RunCommsChannel(object state)
    {
        if (state is not Dictionary<string, object> args)
        {
            return;
        }

        if (args["ea"] is not BasicDeliverEventArgs ea)
        {
            return;
        }

        if (!IsConnectionOpen())
        {
            _logger.LogWarning(
                "Cannot consume response from channel {RoutingKey} - websocket unavailable",
                ea.RoutingKey);

            if (args["model"] is AsyncEventingBasicConsumer consumer)
            {
                await consumer.Channel.BasicRejectAsync(ea.DeliveryTag, true);
            }
            return;
        }

        try
        {
            var result = ea.Body.ToArray();
            var commsResult = Comms.Parser.ParseFrom(result);

            if (commsResult.MessageType == CommsMessageType.OcppResponse)
            {
                var parsed = WampResult.Parser.ParseFrom(commsResult.Payload.ToByteArray());

                if (!parsed.Result.IsEmpty)
                {
                    await RawSend(parsed.Result.ToArray());
                }
                else if (!parsed.Error.IsEmpty)
                {
                    await RawSend(parsed.Error.ToArray());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from {RoutingKey}", ea.RoutingKey);
        }
    }

    public async Task Consume(WebSocket webSocket, string clientIdentifier)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WebSocketDigestionService));
        }

        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _clientIdentifier = clientIdentifier ?? throw new ArgumentNullException(nameof(clientIdentifier));

        _commsMessenger = _commsProvider.GetMessenger("RabbitMQ");
        if (_commsMessenger == null)
        {
            throw new InvalidOperationException("Unable to get RabbitMQ messenger");
        }

        var signalChannelName = _config.GetSection("Comms")["SignalChannel"];
        if (string.IsNullOrEmpty(signalChannelName))
        {
            throw new InvalidOperationException("Signal channel name is not configured");
        }

        try
        {
            var channelName = CommsUtils.GetChannelName(clientIdentifier);
            var responseChannelName = CommsUtils.GetResponseChannelName(channelName);

            await _commsMessenger.RegisterChannel(signalChannelName);
            await _commsMessenger.RegisterChannel(channelName);
            await _commsMessenger.RegisterChannel(responseChannelName);
            await _commsMessenger.RegisterHandler(responseChannelName, RunCommsChannel);

            await _commsMessenger.SendMessage(signalChannelName, new CommunicationChannelSignal
            {
                ChannelName = channelName,
                ChannelType = ChannelType.OcppEvent,
            }.ToByteArray());

            _logger.LogInformation(
                "Client {ClientId} signaled to listen on {Channel} with response on {ResponseChannel}",
                clientIdentifier, channelName, responseChannelName);

            while (!_cts.Token.IsCancellationRequested && IsConnectionOpen())
            {
                if (!IsServiceBusy())
                {
                    var received = await AwaitWebSocketResponse();
                    if (received != null)
                    {
                        _logger.LogInformation(
                            "Received {ByteCount} bytes from {ClientId}",
                            received.Length, clientIdentifier);

                        var payload = new OcppMessage
                        {
                            ClientIdentifier = clientIdentifier,
                            Payload = ByteString.CopyFrom(received),
                        };

                        await _commsMessenger.SendMessage(channelName, payload.ToByteArray());
                    }
                }

                if (_webSocket.CloseStatus.HasValue)
                {
                    _logger.LogWarning(
                        "WS connection for {ClientId} closed. {Status}:{Description}",
                        clientIdentifier, _webSocket.CloseStatus.Value,
                        _webSocket.CloseStatusDescription);
                    break;
                }
            }
        }
        finally
        {
            await DisposeAsync();
        }
    }
}