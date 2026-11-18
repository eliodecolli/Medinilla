using Google.Protobuf;

namespace Medinilla.Core.Events;

/// <summary>
/// Common application-level event emission boundary. Producers supply an already
/// materialized snapshot and a pure payload factory; the emitter owns wrapping,
/// bounded transport publication, failure isolation, and logging.
/// </summary>
public interface IApplicationEventEmitter
{
    Task TryEmitAsync<TPayload>(
        string eventType,
        DateTimeOffset completedAtUtc,
        Func<TPayload> buildPayload,
        CancellationToken cancellationToken = default)
        where TPayload : IMessage<TPayload>;
}
