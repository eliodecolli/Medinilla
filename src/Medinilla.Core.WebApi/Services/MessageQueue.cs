using Medinilla.WebApi.Interfaces;
using System.Collections.Concurrent;

namespace Medinilla.Core.WebApi.Services;

public class MessageQueue(uint ttl = 5) : IMessageQueue
{
    private readonly ConcurrentQueue<byte[]> _queue = new();
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

                // give each call request a TTL
                if (_lastAdded.HasValue && !_queue.IsEmpty) shouldClean = now.Subtract(_lastAdded.Value).TotalSeconds >= ttl;
            }

            if (shouldClean && OnDrainQueue is not null) await OnDrainQueue();
        }
    }

    public bool PopMessage(out byte[] payload)
    {
        return _queue.TryDequeue(out payload!);
    }

    public void EnqueueMessage(byte[] payload)
    {
        lock (_lock)
        {
            _queue.Enqueue(payload);
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
