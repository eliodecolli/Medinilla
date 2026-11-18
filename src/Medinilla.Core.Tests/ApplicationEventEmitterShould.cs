using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Medinilla.Core.Events;
using Medinilla.Core.SharedContracts.Events;
using Medinilla.Infrastructure.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace Medinilla.Core.Tests;

public class ApplicationEventEmitterShould
{
    private static readonly DateTimeOffset CompletedAt =
        new(2026, 11, 14, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task WrapAPurePayloadAndPublishItOnce()
    {
        var publisher = new CapturingPublisher();
        var emitter = new ApplicationEventEmitter(
            publisher,
            NullLogger<ApplicationEventEmitter>.Instance);

        await emitter.TryEmitAsync(
            WebhookEventTypes.ChargerBootup,
            CompletedAt,
            () => new BootupEvent
            {
                ChargerId = "station-1",
                BootReason = "PowerUp",
                VendorName = "Vendor",
                Model = "Model",
            });

        var wrapper = Assert.Single(publisher.Events);
        Assert.Equal(WebhookEventTypes.ChargerBootup, wrapper.EventType);
        Assert.True(Guid.TryParse(wrapper.EventId, out _));
        Assert.Equal(CompletedAt.UtcDateTime, wrapper.EventTimestamp.ToDateTime());

        var payload = BootupEvent.Parser.ParseFrom(wrapper.EventData);
        Assert.Equal("station-1", payload.ChargerId);

        var roundTrip = MedinillaHttpEvent.Parser.ParseFrom(wrapper.ToByteArray());
        Assert.Equal(wrapper.EventId, roundTrip.EventId);
        Assert.Equal(wrapper.EventData, roundTrip.EventData);
    }

    [Fact]
    public async Task IsolateMappingFailures()
    {
        var publisher = new CapturingPublisher();
        var emitter = new ApplicationEventEmitter(
            publisher,
            NullLogger<ApplicationEventEmitter>.Instance);

        await emitter.TryEmitAsync<BootupEvent>(
            WebhookEventTypes.ChargerBootup,
            CompletedAt,
            () => throw new InvalidOperationException("mapping failed"));

        Assert.Empty(publisher.Events);
    }

    [Fact]
    public async Task IsolateTransportFailures()
    {
        var emitter = new ApplicationEventEmitter(
            new ThrowingPublisher(),
            NullLogger<ApplicationEventEmitter>.Instance);

        await emitter.TryEmitAsync(
            WebhookEventTypes.ChargerBootup,
            CompletedAt,
            () => new BootupEvent { ChargerId = "station-1" });
    }

    private sealed class CapturingPublisher : IChargerEventPublisher
    {
        public List<MedinillaHttpEvent> Events { get; } = [];

        public Task PublishAsync(MedinillaHttpEvent httpEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(httpEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPublisher : IChargerEventPublisher
    {
        public Task PublishAsync(MedinillaHttpEvent httpEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Redis unavailable");
    }
}
