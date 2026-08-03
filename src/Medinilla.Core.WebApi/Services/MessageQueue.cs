using System.Collections.Concurrent;
using Medinilla.Core.SharedContracts.Comms.Ocpp;
using Medinilla.WebApi.Interfaces;

namespace Medinilla.Core.WebApi.Services;

public class MessageQueue(uint ttl = 5) : IMessageQueue
{
    private readonly ConcurrentQueue<OcppMessage> _queue = new();
    private readonly Lock _lock = new();
    
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(5));
    private DateTime? _lastAdded;
    
    public Func<Task>? OnDrainQueue { get; set; }

    public async Task ScheduleVacuum(CancellationTokenSource cts)
    {
        while (await _timer.WaitForNextTickAsync(cts.Token))
        {
            var shouldClean = false;
            lock (_lock)
            {
                var now = DateTime.Now;

                // give each call request 5 seconds to be processed
                if (_lastAdded.HasValue && !_queue.IsEmpty) shouldClean = now.Subtract(_lastAdded.Value).TotalSeconds >= ttl;
            }

            if (shouldClean && OnDrainQueue is not null) await OnDrainQueue();
        }
    }

    public bool PopMessage(out OcppMessage message)
    {
        return _queue.TryDequeue(out message!);
    }
    
    public void EnqueueMessage(OcppMessage message)
    {
        lock (_lock)
        {
            _queue.Enqueue(message);
            _lastAdded = DateTime.Now;
        }
    }

    public void Dispose()
    {
        _queue.Clear();
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }
}