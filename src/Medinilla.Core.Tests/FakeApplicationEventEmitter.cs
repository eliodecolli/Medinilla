using Medinilla.Core.Events;
using Google.Protobuf;

namespace Medinilla.Core.Tests;

internal sealed class FakeApplicationEventEmitter : IApplicationEventEmitter
{
    public List<EmittedEvent> Events { get; } = [];

    public Exception? ThrowOnEmit { get; set; }

    public Task TryEmitAsync<TPayload>(
        string eventType,
        DateTimeOffset completedAtUtc,
        Func<TPayload> buildPayload,
        CancellationToken cancellationToken = default)
        where TPayload : IMessage<TPayload>
    {
        if (ThrowOnEmit is not null)
        {
            throw ThrowOnEmit;
        }

        Events.Add(new EmittedEvent(eventType, completedAtUtc, buildPayload()));
        return Task.CompletedTask;
    }

    internal sealed record EmittedEvent(
        string EventType,
        DateTimeOffset CompletedAtUtc,
        object Payload);
}
