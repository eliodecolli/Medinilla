using Google.Protobuf;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Core.SharedContracts.Comms.Ocpp;
using Medinilla.Core.WebApi.Services.Domain;
using Medinilla.Infrastructure;
using Medinilla.Infrastructure.Exceptions;
using Medinilla.RealTime;
using Medinilla.RealTime.Redis;
using Medinilla.WebApi.Interfaces;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace Medinilla.WebApi.Services;

public class WebSocketDigestionService : IBasicWebSocketDigestionService
{
    private readonly IConfiguration _config;
    private readonly ILogger<WebSocketDigestionService> _logger;
    private readonly CancellationTokenSource _cts;

    private WebSocket? _webSocket;
    private string? _clientIdentifier;
    private IRealTimeMessenger _commsMessenger;

    private readonly object _lock;
    private bool _disposed;

    // queue to keep track of inbound charger messages
    private readonly ConcurrentQueue<OcppMessage> _inboundQueue;

    // queue to keep track of outbound messages towards a charger
    private readonly ConcurrentQueue<OcppMessage> _outboundQueue;

    // currently processing message id
    private string? _processingInboundId;
    private DateTime? _inboundTrack;

    private string? _processingOutboundId;
    private DateTime? _outboundTrack;

    // rabbitmq queues

    // Core -> Us
    private string? _inboundQueueName;

    // Us -> Core
    private string? _outboundQueueName;

    // semaphore to coordinate sending data
    private SemaphoreSlim _sendLock;

    private readonly PeriodicTimer _timer;

    public WebSocketDigestionService(
        IConfiguration config,
        ILogger<WebSocketDigestionService> logger,
        ICommunicationProvider provider)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _lock = new object();
        _cts = new CancellationTokenSource();

        _commsMessenger = provider.GetMessenger("Redis") ?? throw new Exception("No Redis Messenger detected");  // TODO: Use custom exception

        _outboundQueue = new ConcurrentQueue<OcppMessage>();
        _inboundQueue = new ConcurrentQueue<OcppMessage>();

        _sendLock = new SemaphoreSlim(1, 1);

        _timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
    }

    #region Assertions
    private void AssertInboundChannelName()
    {
        if (_inboundQueueName is null) throw new NullReferenceException("Inbound Channel Name is not set.");
    }

    private void AssertOutboundChannelName()
    {
        if (_outboundQueueName is null) throw new NullReferenceException("Outbound Channel Name is not set.");
    }

    private void AssertComms()
    {
        if (_commsMessenger is null) throw new NullReferenceException("Redis comms has not been set up.");
    }
    #endregion

    private void SetupChannelNames()
    {
        var outboundPrefix = _config.GetSection("Comms")["RequestQueue"];
        var inboundPrefix = _config.GetSection("Comms")["ResponseQueue"];

        if (outboundPrefix is null) throw new NullReferenceException("'RequestQueue' configuration is not set.");
        if (inboundPrefix is null) throw new NullReferenceException("'ResponseQueue' configguration is not set.");

        if (_clientIdentifier is null) throw new NullReferenceException("Client Identifier is not set.");

        // we only use a single channel to broadcast events
        _outboundQueueName = outboundPrefix;

        _inboundQueueName = RedisUtils.BuildChannelName(inboundPrefix, _clientIdentifier);
    }

    private bool IsConnectionOpen()
    {
        return (_webSocket is not null) && _webSocket.State == WebSocketState.Open;
    }

    private void LogInfo(string message)
    {
        _logger.LogInformation($"[{_clientIdentifier}]: {message}");
    }

    private async Task VacuumPending()
    {
        _logger.LogInformation("OCPP Queue Vacuuming is now initialized.");
        while (await _timer.WaitForNextTickAsync(_cts.Token))
        {
            var shouldCleanInbound = false;
            var shouldCleanOutbound = false;
            lock (_lock)
            {
                var now = DateTime.Now;

                // give each call request 5 seconds to be processed
                if (_inboundTrack.HasValue) shouldCleanInbound = now.Subtract(_inboundTrack.Value).TotalSeconds >= 5;
                if (_outboundTrack.HasValue) shouldCleanOutbound = now.Subtract(_outboundTrack.Value).TotalSeconds >= 5;
            }

            if (shouldCleanInbound) await DrainInbound();
            if (shouldCleanOutbound) await DrainOutbound();
        }
    }

    private void SetProcessingInboundId(string? inboundId)
    {
        if (inboundId is null)
        {
            _logger.LogInformation("Clearing processing inboundd id, previous: {id}", _processingInboundId);
        }
        else
        {
            _logger.LogInformation("Processing inbound id {prev} -> {new}", _processingInboundId, inboundId);
        }

        _processingInboundId = inboundId;
        _inboundTrack = DateTime.Now;
    }

    private void SetProcessingOutboundId(string? outboundId)
    {
        if (outboundId is null)
        {
            _logger.LogInformation("Clearing processing inboundd id, previous: {id}", _processingOutboundId);
        }
        else
        {
            _logger.LogInformation("Processing inbound id {prev} -> {new}", _processingOutboundId, outboundId);
        }

        _processingOutboundId = outboundId;
        _outboundTrack = DateTime.Now;
    }

    private OcppHeader GetMessageHeader(string rawMessage)
    {
        try
        {
            var parser = new OcppMessageParser();
            parser.LoadRaw(rawMessage);

            var messageId = parser.TryExtractMessageId();
            if (messageId is null)
            {
                throw new InvalidOcppMessageException(_clientIdentifier ?? "<Unkown>");
            }

            var type = parser.GetMessageType() switch
            {
                Infrastructure.WAMP.OcppJMessageType.CALL => CommsMessageType.OcppRequest,
                Infrastructure.WAMP.OcppJMessageType.CALL_RESULT or Infrastructure.WAMP.OcppJMessageType.CALL_ERROR => CommsMessageType.OcppResponse,
                _ => throw new NotImplementedException(),
            };

            return new OcppHeader(type, messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error while peeking message ID: {error}", ex.Message);
            throw;
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

        lock (_lock)
        {
            if (_outboundQueue.TryDequeue(out var next))
            {
                var rawMessage = Encoding.UTF8.GetString(next.Payload.ToByteArray());
                var header = GetMessageHeader(rawMessage);

                LogInfo($"Dequeueing message: {header.MessageId} (outbound)");

                // sanity check - because if it was enqueued the caller has alreaddy checked if it's null or not
                if (!string.IsNullOrEmpty(header.MessageId))
                {
                    SetProcessingOutboundId(header.MessageId);
                }
                toSend = next.Payload.ToByteArray();
            }
        }

        if (toSend is not null) await SendRaw(toSend);
    }

    private async Task DrainInbound()
    {
        Comms? toSend = null;

        lock (_lock)
        {
            if (_inboundQueue.TryDequeue(out var next))
            {
                var rawMessage = Encoding.UTF8.GetString(next.Payload.ToByteArray());
                var header = GetMessageHeader(rawMessage);

                LogInfo($"Dequeueing message: {header.MessageId} (inbound)");

                // sanity check - because if it was enqueued the caller has alreaddy checked if it's null or not
                if (!string.IsNullOrEmpty(header.MessageId))
                {
                    SetProcessingInboundId(header.MessageId);
                }

                toSend = new Comms() { MessageType = header.Type, Payload = next.ToByteString() };
            }
        }
        if (toSend is not null) await PublishCommsMessage(toSend);
    }

    private async Task PublishCommsMessage(Comms message)
    {
        AssertComms();
        AssertOutboundChannelName();

        await _commsMessenger!.SendMessage(_outboundQueueName!, message.ToByteArray());
        _logger.LogInformation($"Comms: Sent {Enum.GetName(message.MessageType)} to {_outboundQueueName}");
    }


    private async Task<byte[]?> WebSocketResponse()
    {
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
        catch (WebSocketException)
        {
            // websocket probably closed up on us unexpectedly
            return null;
        }
    }

    private async Task RunCommsChannel(object state)
    {
        _logger.LogInformation("Received from {rabbit}", _inboundQueueName);

        try
        {
            var result = (byte[])state;
            var commsResult = Comms.Parser.ParseFrom(result);

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
            var header = GetMessageHeader(rawString);

            var shouldDrainInbound = false;
            var shouldForward = false;

            lock (_lock)
            {
                if (commsResult.MessageType == CommsMessageType.OcppResponse)
                {

                    if (_processingInboundId == header.MessageId)
                    {
                        // boy have we got a response for you :)
                        _processingInboundId = null;
                        shouldDrainInbound = true;
                        shouldForward = true;
                    }
                    else
                    {
                        _logger.LogWarning($"Received message {header.MessageId} of type {Enum.GetName(header.Type)} - but it doesn't match our inbound ID");
                    }
                }
                // only  check this when we're trying to send a request
                else if (header.Type == CommsMessageType.OcppRequest)
                {
                    if (_processingOutboundId is null)
                    {
                        _processingOutboundId = header.MessageId;
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from {RoutingKey}", _inboundQueueName);
        }
    }

    private async Task ProcessMessageInbound(byte[] message)
    {
        // first of all parse the message so we can peek at the current
        var rawStringMessage = Encoding.UTF8.GetString(message);
        var header = GetMessageHeader(rawStringMessage);

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
            if (_processingOutboundId == header.MessageId)
            {
                SetProcessingOutboundId(null);
                shouldDrainOutbound = true;
                shouldForward = true;
            }

            else if (header.Type == CommsMessageType.OcppRequest)
            {
                if (_processingInboundId is null)
                {
                    SetProcessingInboundId(header.MessageId);
                    shouldForward = true;
                }
                else
                {
                    _inboundQueue.Enqueue(payload);
                    _logger.LogWarning("Protocol violation from {clientId}: Charger sent us a new request before completion of the current one.", _clientIdentifier);
                }
            }

            else
            {
                _logger.LogWarning($"Received message {header.MessageId} of type {Enum.GetName(header.Type)} - but it doesn't match our outbound ID");
            }
        }

        if (shouldForward) await PublishCommsMessage(new Comms() { MessageType = header.Type, Payload = payload.ToByteString() });
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

        if (_commsMessenger == null)
        {
            throw new InvalidOperationException("Unable to get RabbitMQ messenger");
        }

        SetupChannelNames();

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

        var vacuumTask = Task.Run(VacuumPending);

        while (!_cts.Token.IsCancellationRequested && IsConnectionOpen())
        {
            if (_webSocket.CloseStatus.HasValue)
            {
                _logger.LogWarning(
                    "WS connection for {ClientId} closed. {Status}:{Description}",
                    clientIdentifier, _webSocket.CloseStatus.Value,
                    _webSocket.CloseStatusDescription);
                break;
            }

            var received = await WebSocketResponse();
            if (received != null)
            {
                _logger.LogInformation(
                    "Received {ByteCount} bytes from {ClientId}",
                    received.Length, clientIdentifier);

                await ProcessMessageInbound(received);
            }
        }

        _cts.Cancel();

        try
        {
            await vacuumTask;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Vacuum Task ended.");
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
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }

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
            _timer.Dispose();
        }
    }
}