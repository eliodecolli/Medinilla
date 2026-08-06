namespace Medinilla.WebApi.Interfaces;

public interface IMessageQueue : IDisposable
{
    Func<Task>? OnDrainQueue { get; set; }

    Task ScheduleVacuum(CancellationTokenSource cts);

    bool PopMessage(out byte[] payload);

    void EnqueueMessage(byte[] payload);
}
