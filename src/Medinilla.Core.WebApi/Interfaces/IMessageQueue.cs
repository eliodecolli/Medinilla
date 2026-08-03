using Medinilla.Core.SharedContracts.Comms.Ocpp;

namespace Medinilla.WebApi.Interfaces;

public interface IMessageQueue : IDisposable
{
    Func<Task>? OnDrainQueue { get; set; }

    Task ScheduleVacuum(CancellationTokenSource cts);

    bool PopMessage(out OcppMessage message);

    void EnqueueMessage(OcppMessage message);
}
