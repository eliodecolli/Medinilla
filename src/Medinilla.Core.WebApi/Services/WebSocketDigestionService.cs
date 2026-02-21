using Google.Protobuf;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Core.SharedContracts.Comms.Ocpp;
using Medinilla.Infrastructure;
using Medinilla.RealTime;
using Medinilla.WebApi.Interfaces;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace Medinilla.WebApi.Services;

public class WebSocketDigestionService : IBasicWebSocketDigestionService, IAsyncDisposable
{
    private readonly IConfiguration _config;
    private readonly ILogger<WebSocketDigestionService> _logger;
    private readonly ICommunicationProvider _commsProvider;
    private readonly CancellationTokenSource _cts;

    private WebSocket? _webSocket;
    private string? _clientIdentifier;
    private IRealTimeMessenger? _commsMessenger;

    private readonly object _lock;
    private bool _disposed;

    // queue to keep track of inbound charger messages
    private readonly ConcurrentQueue<OcppMessage> _inboundQueue;

    // queue to keep track of outbound messages towards a charger
    private readonly ConcurrentQueue<OcppMessage> _outboundQueue;

    // currently processing message id
    private string? _processingInboundId;
    private string? _processingOutboundId;


    // rabbitmq queues

    // Core -> Us
    private string? _inboundQueueName;

    // Us -> Core
    private string? _outboundQueueName;

    // semaphore to coordinate sending data
    private SemaphoreSlim _sendLock;

    public WebSocketDigestionService(
        IConfiguration config,
        ILogger<WebSocketDigestionService> logger,
        ICommunicationProvider commsProvider)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _commsProvider = commsProvider ?? throw new ArgumentNullException(nameof(commsProvider));

        _lock = new object();
        _cts = new CancellationTokenSource();

        _commsMessenger = _commsProvider.GetMessenger("RabbitMQ");

        _outboundQueue = new ConcurrentQueue<OcppMessage>();
        _inboundQueue = new ConcurrentQueue<OcppMessage>();

        _sendLock = new SemaphoreSlim(1, 1);
    }




    private bool IsConnectionOpen()
    {
        return (_webSocket is not null) && _webSocket.State == WebSocketState.Open;
    }

    private string? PeekMessageCorrelationId(string rawMessage)
    {
        try
        {
            var parser = new OcppMessageParser();
            parser.LoadRaw(rawMessage);

            return parser.TryExtractMessageId();
        }
        catch (Exception ex)
        {
            _logger.LogError("Error while peeking message ID: {error}", ex.Message);
            return null;
        }
    }

    private async Task SendRaw(byte[] data)
    {
        if (IsConnectionOpen())
        {
            await _sendLock.WaitAsync();
            try
            {
                await _webSocket!.SendAsync(data, WebSocketMessageType.Text, true, _cts.Token);
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }

    private async Task DrainOutbound()
    {
        byte[]? toSend = null;

        lock(_lock)
        {
            if (_outboundQueue.TryDequeue(out var next))
            {
                var rawMessage = Encoding.UTF8.GetString(next.Payload.ToByteArray());
                var messageId = PeekMessageCorrelationId(rawMessage);

                // sanity check - because if it was enqueued the caller has alreaddy checked if it's null or not
                if (!string.IsNullOrEmpty(messageId))
                {
                    _processingOutboundId = messageId;
                }
                toSend = next.Payload.ToByteArray();
            }
        }

        if (toSend is not null) await SendRaw(toSend);
    }

    private async Task DrainInbound()
    {
        byte[]? toSend = null;

        lock(_lock)
        {
            if (_inboundQueue.TryDequeue(out var next))
            {
                var rawMessage = Encoding.UTF8.GetString(next.Payload.ToByteArray());
                var messageId = PeekMessageCorrelationId(rawMessage);

                // sanity check - because if it was enqueued the caller has alreaddy checked if it's null or not
                if (!string.IsNullOrEmpty(messageId))
                {
                    _processingInboundId = messageId;
                }

                toSend = next.Payload.ToByteArray();
            }
        }
        if (toSend is not null) await ForwardToRabbit(toSend);
    }

    private async Task ForwardToRabbit(byte[] message)
    {
        if (_commsMessenger is not null)
        {
            await _commsMessenger.SendMessage(_outboundQueueName, message);
        }
    }


    private async Task<byte[]?> AwaitWebSocketResponse()
    {
        if (_webSocket == null || !IsConnectionOpen())
        {
            return null;
        }

        var buffer = new byte[5000];
        var segment = new ArraySegment<byte>(buffer);
        var messageBuffer = new List<byte>(); // To store the complete message

        try
        {
            WebSocketReceiveResult result;
            do
            {
                // Receive a frame
                result = await _webSocket.ReceiveAsync(segment, _cts.Token);

                if (result.CloseStatus.HasValue)
                {
                    _logger.LogWarning("WS connection for {ClientId} has been closed. {Status}:{Description}",
                        _clientIdentifier, result.CloseStatus.Value, result.CloseStatusDescription);
                    return null;
                }

                // Add the received data to our message buffer
                messageBuffer.AddRange(segment.AsSpan(0, result.Count).ToArray());

                // Continue until we've received the entire message
            } while (!result.EndOfMessage);

            return messageBuffer.ToArray();
        }
        catch (OperationCanceledException)
        {
            return null;
        }
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

        try
        {
            var result = ea.Body.ToArray();
            var commsResult = Comms.Parser.ParseFrom(result);

            if (commsResult.MessageType == CommsMessageType.OcppResponse)
            {
                var parsed = WampResult.Parser.ParseFrom(commsResult.Payload.ToByteArray());
                if (parsed.ClientIdentifier != _clientIdentifier)
                {
                    return;  // not our guy
                }

                if (!IsConnectionOpen())
                {
                    return;  // yeah we're done here pal
                }

                byte[]? message = null;

                if (!parsed.Result.IsEmpty)
                {
                    message = parsed.Result.ToByteArray();
                }
                else if (!parsed.Error.IsEmpty)
                {
                    message = parsed.Error.ToByteArray();
                }

                var rawString = Encoding.UTF8.GetString(message ?? []);
                var messageId = PeekMessageCorrelationId(rawString);

                if (string.IsNullOrEmpty(messageId))
                {
                    _logger.LogError("Invalid response from core service for client {clientId}", _clientIdentifier);
                    return;
                }

                var shouldDrainInbound = false;
                var shouldForward = false;

                lock(_lock)
                {
                    if (_processingInboundId == messageId)
                    {
                        // boy have we got a response for you :)
                        _processingInboundId = null;
                        shouldDrainInbound = true;
                        shouldForward = true;
                    }
                    else
                    {
                        if (_processingOutboundId is null)
                        {
                            _processingOutboundId = messageId;
                            shouldForward = true;
                        }
                        else
                        {
                            _outboundQueue.Enqueue(new OcppMessage()
                            {
                                ClientIdentifier = _clientIdentifier,
                                Payload = ByteString.CopyFrom(message)
                            });
                        }
                    }
                }

                if (shouldForward && message is not null) await SendRaw(message);
                if (shouldDrainInbound) await DrainInbound();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from {RoutingKey}", ea.RoutingKey);
        }
    }

    private async Task ProcessMessageInbound(byte[] message)
    {
        // first of all parse the message so we can peek at the current
        var rawStringMessage = Encoding.UTF8.GetString(message);
        var messageId = PeekMessageCorrelationId(rawStringMessage);

        if (messageId is null)
        {
            _logger.LogError("Invalid message from client {clientId}", _clientIdentifier);
            return;
        }

        var payload = new OcppMessage
        {
            ClientIdentifier = _clientIdentifier,
            Payload = ByteString.CopyFrom(message),
        };

        // now check if the message matches the pending outboound message
        var shouldDrainOutbound = false;
        var shouldForward = false;
        lock (_lock)
        {
            if (_processingOutboundId == messageId)
            {
                _processingOutboundId = null;
                shouldDrainOutbound = true;
                shouldForward = true;
            }
            else
            {
                if (_processingInboundId is null)
                {
                    _processingInboundId = messageId;
                    shouldForward = true;
                }
                else
                {
                    _inboundQueue.Enqueue(payload);
                    _logger.LogWarning("Protocol violation from {clientId}: Charger sent us a new request before completion of the current one.", _clientIdentifier);
                }
            }
        }

        if (shouldForward) await ForwardToRabbit(payload.ToByteArray());
        if (shouldDrainOutbound) await DrainOutbound();
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

        _outboundQueueName = _config.GetSection("Comms")["RequestQueue"];
        _inboundQueueName = _config.GetSection("Comms")["ResponseQueue"];


        if (string.IsNullOrEmpty(_inboundQueueName))
        {
            throw new InvalidOperationException("Request queue name is not configured");
        }

        if (string.IsNullOrEmpty(_outboundQueueName))
        {
            throw new InvalidOperationException("Response queue name is not configured");
        }

        await _commsMessenger.RegisterChannel(_outboundQueueName);
        await _commsMessenger.RegisterChannel(_inboundQueueName);

        await _commsMessenger.RegisterHandler(_inboundQueueName, RunCommsChannel);

        try
        {
            while (!_cts.Token.IsCancellationRequested && IsConnectionOpen())
            {
                var received = await AwaitWebSocketResponse();
                if (received != null)
                {
                    _logger.LogInformation(
                        "Received {ByteCount} bytes from {ClientId}",
                        received.Length, clientIdentifier);

                    await ProcessMessageInbound(received);
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during service disposal");
        }
        finally
        {
            _inboundQueue.Clear();
            _outboundQueue.Clear();
            _sendLock.Dispose();
            _cts.Dispose();
        }
    }
}