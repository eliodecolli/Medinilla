using System.Net.WebSockets;
using System.Text;
using Google.Protobuf;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Core.WebApi.Services.Domain;
using Medinilla.Infrastructure;
using Medinilla.Infrastructure.Exceptions;
using Medinilla.RealTime.Redis;
using Medinilla.WebApi.Interfaces;

namespace Medinilla.Core.WebApi.Services;

public class WebSocketDigestionService : IBasicWebSocketDigestionService
{
    private readonly IConfiguration _config;
    private readonly ILogger<WebSocketDigestionService> _logger;
    private readonly CancellationTokenSource _cts;

    private WebSocket? _webSocket;
    private string? _clientIdentifier;

    private readonly IInternalCommunicationService _comms;

    private bool _disposed;

    // currently processing message id
    private string? _processingInboundId;
    private string? _processingOutboundId;

    // RealTime queues
    // Core -> Us
    private string? _inboundQueueName;

    // Us -> Core
    private string? _outboundQueueName;

    // semaphore to coordinate sending data
    private readonly SemaphoreSlim _sendLock;

    private readonly Lock _lock = new Lock();

    private readonly IMessageQueue _inbound;
    private readonly IMessageQueue _outbound;

    private const string InboundDirection = "Inbound";
    private const string OutboundDirection = "Outbound";

    public WebSocketDigestionService(
        IConfiguration config,
        ILogger<WebSocketDigestionService> logger,
        IInternalCommunicationService comms,
        IMessageQueueFactory queueFactory)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _comms = comms ?? throw new ArgumentNullException(nameof(comms));
        ArgumentNullException.ThrowIfNull(queueFactory);

        _inbound = queueFactory.Create();
        _outbound = queueFactory.Create();

        _cts = new CancellationTokenSource();

        _sendLock = new SemaphoreSlim(1, 1);

        _inbound.OnDrainQueue = DrainInbound;
        _outbound.OnDrainQueue = DrainOutbound;
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
    #endregion

    #region Setup
    private void SetupChannelNames()
    {
        var outboundPrefix = _config.GetSection("Comms")["RequestQueue"];
        var inboundPrefix = _config.GetSection("Comms")["ResponseQueue"];

        if (outboundPrefix is null) throw new InvalidOperationException("'RequestQueue' configuration is not set.");
        if (inboundPrefix is null) throw new InvalidOperationException("'ResponseQueue' configuration is not set.");

        if (_clientIdentifier is null) throw new NullReferenceException("Client Identifier is not set.");

        // we only use a single channel to broadcast events
        _outboundQueueName = outboundPrefix;

        _inboundQueueName = RedisUtils.BuildChannelName(inboundPrefix, _clientIdentifier);
    }

    private bool IsConnectionOpen()
    {
        return (_webSocket is not null) && _webSocket.State == WebSocketState.Open;
    }
    #endregion

    #region State Tracking
    private void SetProcessingInboundId(string? inboundId)
    {
        if (inboundId is null)
        {
            _logger.LogInformation("[{ClientId}] Clearing processing inbound id, previous: {id}", _clientIdentifier, _processingInboundId);
        }
        else
        {
            _logger.LogInformation("[{ClientId}] Processing inbound id {prev} -> {new}", _clientIdentifier, _processingInboundId, inboundId);
        }

        _processingInboundId = inboundId;
    }

    private void SetProcessingOutboundId(string? outboundId)
    {
        if (outboundId is null)
        {
            _logger.LogInformation("[{ClientId}] Clearing processing outbound id, previous: {id}", _clientIdentifier, _processingOutboundId);
        }
        else
        {
            _logger.LogInformation("[{ClientId}] Processing outbound id {prev} -> {new}", _clientIdentifier, _processingOutboundId, outboundId);
        }

        _processingOutboundId = outboundId;
    }
    #endregion

    #region Message Processing
    private OcppHeader GetMessageHeader(byte[] rawMessage)
    {
        try
        {
            var rawString = Encoding.UTF8.GetString(rawMessage);
            var parser = new OcppMessageParser();
            parser.LoadRaw(rawString);

            var messageId = parser.TryExtractMessageId();
            if (messageId is null)
            {
                throw new InvalidOcppMessageException(_clientIdentifier ?? "Unknown");
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
            _logger.LogError("[{ClientId}] Error while peeking message ID: {error}", _clientIdentifier, ex.Message);
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

    private async Task<byte[]?> WebSocketResponse()
    {
        var buffer = new byte[5000];
        var segment = new ArraySegment<byte>(buffer);
        var messageBuffer = new List<byte>(); // To store the complete message

        if (_webSocket is null)
        {
            throw new InvalidOperationException($"{_clientIdentifier}: WebSocket is not defined.");
        }

        try
        {
            WebSocketReceiveResult result;
            do
            {
                // Receive a frame
                result = await _webSocket.ReceiveAsync(segment, _cts.Token);

                if (result.CloseStatus.HasValue)
                {
                    _logger.LogWarning("[{ClientId}] WS connection has been closed. {Status}:{Description}",
                        _clientIdentifier, result.CloseStatus.Value, result.CloseStatusDescription);
                    return null;
                }

                // Add the received data to our message buffer
                messageBuffer.AddRange([.. segment.AsSpan(0, result.Count)]);

                // Continue until we've received the entire message
            } while (!result.EndOfMessage);

            return [.. messageBuffer];
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
    #endregion

    #region Queue Management
    private async Task DrainQueue(
        IMessageQueue queue,
        string direction,
        Func<OcppHeader, byte[], Task> processor)
    {
        byte[]? payload = null;
        OcppHeader? header = null;

        lock (_lock)
        {
            if (queue.PopMessage(out var next))
            {
                header = GetMessageHeader(next);

                _logger.LogInformation("[{ClientId}] De-queueing message: {MessageId} ({Direction})",
                    _clientIdentifier, header.MessageId, direction.ToLowerInvariant());

                if (!string.IsNullOrEmpty(header.MessageId))
                {
                    if (direction == InboundDirection) SetProcessingInboundId(header.MessageId);
                    else                              SetProcessingOutboundId(header.MessageId);
                }
                payload = next;
            }
        }

        if (payload is not null && header is not null)
            await processor(header, payload);
    }

    private Task DrainInbound()
        => DrainQueue(_inbound, InboundDirection, (_, payload) =>
            PublishCommsMessage(new Comms
            {
                MessageType = CommsMessageType.OcppRequest,
                ClientIdentifier = _clientIdentifier!,
                Payload = ByteString.CopyFrom(payload),
            }));

    private Task DrainOutbound()
        => DrainQueue(_outbound, OutboundDirection, (_, payload) => SendRaw(payload));

    private Task PublishCommsMessage(Comms message)
        => _comms.PublishCommsMessage(message);

    private async Task OnComms(Comms comms)
    {
        if (!IsConnectionOpen())
        {
            // stop the comms but finish processing the message
            _comms.Stop();
        }

        var message = comms.Payload.ToByteArray();
        var rawString = Encoding.UTF8.GetString(message);
        var header = GetMessageHeader(message);

        var shouldDrainInbound = false;
        var shouldForward = false;

        lock (_lock)
        {
            if (header.Type == CommsMessageType.OcppResponse)
            {
                if (_processingInboundId == header.MessageId)
                {
                    _processingInboundId = null;
                    shouldDrainInbound = true;
                    shouldForward = true;
                }
                else
                {
                    _logger.LogWarning(
                        "[{ClientId}] Received message {MessageId} of type {Type} - but it doesn't match our inbound ID",
                        _clientIdentifier, header.MessageId, Enum.GetName(header.Type));
                }
            }
            else if (header.Type == CommsMessageType.OcppRequest)
            {
                if (_processingOutboundId is null)
                {
                    _processingOutboundId = header.MessageId;
                    shouldForward = true;

                    _logger.LogInformation("[{ci}]: Sending Raw: {raw}", _clientIdentifier, rawString);
                }
                else
                {
                    _outbound.EnqueueMessage(message);
                }
            }
        }

        if (shouldForward) await SendRaw(message);
        if (shouldDrainInbound) await DrainInbound();
    }

    private async Task ProcessMessageInbound(byte[] message)
    {
        var header = GetMessageHeader(message);

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
                    _inbound.EnqueueMessage(message);
                    _logger.LogWarning("[{ClientId}] Protocol violation: Charger sent us a new request before completion of the current one.", _clientIdentifier);
                }
            }

            else
            {
                _logger.LogWarning("[{ClientId}] Received message {MessageId} of type {Type} - but it doesn't match our outbound ID",
                    _clientIdentifier, header.MessageId, Enum.GetName(header.Type));
            }
        }

        if (shouldForward) await PublishCommsMessage(new Comms
        {
            MessageType = header.Type,
            ClientIdentifier = _clientIdentifier!,
            Payload = ByteString.CopyFrom(message),
        });
        if (shouldDrainOutbound) await DrainOutbound();
    }
    #endregion

    #region Disposal
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
            _logger.LogError(ex, "[{ClientId}] Error during service disposal", _clientIdentifier ?? "<unknown>");
        }
        finally
        {
            _inbound.Dispose();
            _outbound.Dispose();
            _sendLock.Dispose();
            _cts.Dispose();
            await _comms.DisposeAsync();
        }
    }
    #endregion

    public async Task Consume(WebSocket webSocket, string clientIdentifier)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WebSocketDigestionService));
        }

        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _clientIdentifier = clientIdentifier ?? throw new ArgumentNullException(nameof(clientIdentifier));

        SetupChannelNames();

        if (string.IsNullOrEmpty(_inboundQueueName))
        {
            throw new InvalidOperationException("Request queue name is not configured");
        }

        if (string.IsNullOrEmpty(_outboundQueueName))
        {
            throw new InvalidOperationException("Response queue name is not configured");
        }

        _comms.Start(_clientIdentifier, _inboundQueueName!, _outboundQueueName!, OnComms);

        var inboundVacuum = Task.Run(() => _inbound.ScheduleVacuum(_cts));
        var outboundVacuum = Task.Run(() => _outbound.ScheduleVacuum(_cts));

        while (!_cts.Token.IsCancellationRequested && IsConnectionOpen())
        {
            if (_webSocket.CloseStatus.HasValue)
            {
                _logger.LogWarning(
                    "[{ClientId}] WS connection closed. {Status}:{Description}",
                    clientIdentifier, _webSocket.CloseStatus.Value,
                    _webSocket.CloseStatusDescription);
                break;
            }

            var received = await WebSocketResponse();
            if (received != null)
            {
                _logger.LogInformation(
                    "[{ClientId}] Received {ByteCount} bytes",
                    clientIdentifier, received.Length);

                try
                {
                    await ProcessMessageInbound(received);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{ClientId}] Error processing message — dropping.", clientIdentifier);
                }
            }
        }

        await _cts.CancelAsync();

        try
        {
            await Task.WhenAll(inboundVacuum, outboundVacuum);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("[{ClientId}] Vacuum Task ended.", _clientIdentifier);
        }

        _comms.Stop();
    }
}
