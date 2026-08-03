using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Core.SharedContracts.Comms.Ocpp;

namespace Medinilla.WebApi.Interfaces;

public interface IInternalCommunicationService : IAsyncDisposable
{
    void Start(
        string clientIdentifier,
        string inboundQueueName,
        string outboundQueueName,
        Func<WampResult, Task> onMessage);

    void Stop();

    Task PublishCommsMessage(Comms message);
}
