using Medinilla.Core.SharedContracts.Comms;

namespace Medinilla.WebApi.Interfaces;

public interface IInternalCommunicationService : IAsyncDisposable
{
    void Start(
        string clientIdentifier,
        string inboundQueueName,
        string outboundQueueName,
        Func<Comms, Task> onMessage);

    void Stop();

    Task PublishCommsMessage(Comms message);
}
