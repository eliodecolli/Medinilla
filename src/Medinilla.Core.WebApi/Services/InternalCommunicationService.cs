using Google.Protobuf;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.RealTime;
using Medinilla.WebApi.Interfaces;

namespace Medinilla.Core.WebApi.Services;

public class InternalCommunicationService : IInternalCommunicationService
{
    private readonly ISender _sender;
    private readonly IReceiver _receiver;
    private readonly ILogger<InternalCommunicationService> _logger;

    private readonly CancellationTokenSource _cts = new();

    private bool _disposed;
    private bool _stopped;
    private bool _started;

    private string? _clientIdentifier;
    private string? _inboundQueueName;
    private string? _outboundQueueName;
    private Func<Comms, Task>? _onMessage;

    public InternalCommunicationService(
        ISender sender,
        IReceiver receiver,
        ILogger<InternalCommunicationService> logger)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Start(
        string clientIdentifier,
        string inboundQueueName,
        string outboundQueueName,
        Func<Comms, Task> onMessage)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(InternalCommunicationService));
        if (_stopped) throw new InvalidOperationException("Service has been stopped and cannot be restarted.");
        if (_started) throw new InvalidOperationException("Service has already been started.");

        _clientIdentifier = clientIdentifier ?? throw new ArgumentNullException(nameof(clientIdentifier));
        _inboundQueueName = inboundQueueName ?? throw new ArgumentNullException(nameof(inboundQueueName));
        _outboundQueueName = outboundQueueName ?? throw new ArgumentNullException(nameof(outboundQueueName));
        _onMessage = onMessage ?? throw new ArgumentNullException(nameof(onMessage));

        _started = true;

        _ = Task.Run(RunCommsChannel);
    }

    public void Stop()
    {
        if (_stopped || _disposed) return;

        _stopped = true;

        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
    }

    public async Task PublishCommsMessage(Comms message)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(InternalCommunicationService));
        if (_outboundQueueName is null) throw new InvalidOperationException("Outbound queue name has not been set.");

        await _sender.SendAsync(_outboundQueueName, message.ToByteArray());

        _logger.LogInformation(
            "[{ClientId}] Comms: Sent {Type} to {Queue}",
            _clientIdentifier,
            Enum.GetName(message.MessageType),
            _outboundQueueName);
    }

    private async Task RunCommsChannel()
    {
        while (!_cts.Token.IsCancellationRequested && !_stopped && !_disposed)
        {
            byte[]? result;
            try
            {
                result = await _receiver.ReceiveAsync(_inboundQueueName!, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_stopped || _disposed) break;

            _logger.LogInformation(
                "[{ClientId}] Received from {Queue}",
                _clientIdentifier,
                _inboundQueueName);

            try
            {
                if (result is not null && _onMessage is not null)
                {
                    var comms = Comms.Parser.ParseFrom(result);
                    await _onMessage(comms);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[{ClientId}] Error processing message from {Queue}",
                    _clientIdentifier,
                    _inboundQueueName);
            }
        }
        _logger.LogInformation("[{ci}]: ================ Service stopped. ================ ", _clientIdentifier);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;
        Stop();
        _cts.Dispose();

        await ValueTask.CompletedTask;
    }
}
