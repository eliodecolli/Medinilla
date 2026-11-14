using System.Diagnostics;
using Google.Protobuf;
using Medinilla.Core.Events;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.SharedContracts.Events;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.Enums;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Medinilla.DataAccess.Relational.Models.ChargerConfig;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure;
using Medinilla.Infrastructure.Events;
using Medinilla.Infrastructure.WAMP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using DbChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;
using OcppChargingStation = Medinilla.DataTypes.Contracts.Common.ChargingStation;

namespace Medinilla.Core.Tests;

public class ProcessedEventPublisherShould : IDisposable
{
    private const string ChargerId = "station-1";
    private static readonly DateTime CompletionTime = new(2026, 11, 14, 10, 30, 0, DateTimeKind.Utc);

    private readonly TestDbContext _context;
    private readonly CapturingPublisher _publisher = new();
    private readonly Mock<IConsumptionService> _consumption = new();
    private readonly ProcessedEventPublisher _publisherService;

    public ProcessedEventPublisherShould()
    {
        var options = new DbContextOptionsBuilder<MedinillaOcppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new TestDbContext(options);
        _publisherService = new ProcessedEventPublisher(
            _publisher,
            new BootupEventMapper(),
            new StatusNotificationEventMapper(_context, NullLogger<StatusNotificationEventMapper>.Instance),
            new TransactionLifecycleEventMapper(
                _context,
                _consumption.Object,
                NullLogger<TransactionLifecycleEventMapper>.Instance),
            NullLogger<ProcessedEventPublisher>.Instance);
    }

    [Fact]
    public async Task PublishAcceptedBootupWithAnIndependentPayload()
    {
        var request = new BootNotificationRequest(
            BootReasonEnum.PowerUp,
            new OcppChargingStation
            {
                VendorName = "Vendor",
                Model = "Model",
                SerialNumber = "serial-1",
                FirmwareVersion = "1.2.3",
            });
        var call = Call(OcppActionNames.BootNotification, request);
        var result = Result(new BootNotificationResponse(60, RegistrationStatusEnum.Accepted, null));

        await _publisherService.PublishForCallAsync(call, ChargerId, result, CompletionTime);

        var wrapper = Assert.Single(_publisher.Events);
        Assert.Equal(WebhookEventTypes.ChargerBootup, wrapper.EventType);
        Assert.True(Guid.TryParse(wrapper.EventId, out _));
        Assert.Equal(CompletionTime, wrapper.EventTimestamp.ToDateTime());

        var roundTrip = MedinillaHttpEvent.Parser.ParseFrom(wrapper.ToByteArray());
        Assert.Equal(wrapper.EventId, roundTrip.EventId);
        Assert.Equal(wrapper.EventType, roundTrip.EventType);
        Assert.Equal(wrapper.EventTimestamp, roundTrip.EventTimestamp);
        Assert.Equal(wrapper.EventData, roundTrip.EventData);

        var payload = BootupEvent.Parser.ParseFrom(wrapper.EventData);
        Assert.Equal(ChargerId, payload.ChargerId);
        Assert.Equal("PowerUp", payload.BootReason);
        Assert.Equal("Vendor", payload.VendorName);
        Assert.Equal("Model", payload.Model);
        Assert.Equal("serial-1", payload.SerialNumber);
        Assert.Equal("1.2.3", payload.FirmwareVersion);
    }

    [Fact]
    public async Task NotPublishRejectedBootup()
    {
        var request = new BootNotificationRequest(
            BootReasonEnum.PowerUp,
            new OcppChargingStation { VendorName = "Vendor", Model = "Model" });
        var call = Call(OcppActionNames.BootNotification, request);
        var result = Result(new BootNotificationResponse(60, RegistrationStatusEnum.Rejected, null));

        await _publisherService.PublishForCallAsync(call, ChargerId, result, CompletionTime);

        Assert.Empty(_publisher.Events);
    }

    [Fact]
    public async Task NotPublishWhenTheActionReturnedAnOcppError()
    {
        var request = new BootNotificationRequest(
            BootReasonEnum.PowerUp,
            new OcppChargingStation { VendorName = "Vendor", Model = "Model" });
        var call = Call(OcppActionNames.BootNotification, request);
        var result = new RpcResult
        {
            Error = new OcppCallError("msg-1", OcppCallError.ErrorCodes.InternalError, "failed"),
            ReturnToCS = true,
        };

        await _publisherService.PublishForCallAsync(call, ChargerId, result, CompletionTime);

        Assert.Empty(_publisher.Events);
    }

    [Fact]
    public async Task NotThrowWhenTheTransportPublisherFails()
    {
        var service = new ProcessedEventPublisher(
            new ThrowingPublisher(),
            new BootupEventMapper(),
            new StatusNotificationEventMapper(_context, NullLogger<StatusNotificationEventMapper>.Instance),
            new TransactionLifecycleEventMapper(
                _context,
                _consumption.Object,
                NullLogger<TransactionLifecycleEventMapper>.Instance),
            NullLogger<ProcessedEventPublisher>.Instance);
        var request = new BootNotificationRequest(
            BootReasonEnum.PowerUp,
            new OcppChargingStation { VendorName = "Vendor", Model = "Model" });

        await service.PublishForCallAsync(
            Call(OcppActionNames.BootNotification, request),
            ChargerId,
            Result(new BootNotificationResponse(60, RegistrationStatusEnum.Accepted, null)),
            CompletionTime);

        // Reaching this point without an exception is the assertion: publication
        // failure must not propagate into charger processing.
    }

    [Fact]
    public async Task PublishEveryProcessedStatusNotificationAndEnrichKnownConnectorType()
    {
        var station = new DbChargingStation
        {
            Id = Guid.NewGuid(),
            ClientIdentifier = ChargerId,
            Model = "Model",
            Vendor = "Vendor",
            LatestBootNotificationReason = "PowerUp",
        };
        var connector = new EvseConnector
        {
            Id = Guid.NewGuid(),
            ChargingStationId = station.Id,
            EvseId = 2,
            ConnectorId = 1,
            ConnectorStatus = "Available",
            ModifiedAt = DateTime.UtcNow,
        };
        var component = new ChargerComponent
        {
            ChargingStationId = station.Id,
            ChargingStation = station,
            EvseConnectorId = connector.Id,
            Connector = connector,
            ClientIdentifier = ChargerId,
            ComponentName = "Connector",
            ComponentVariables =
            [
                new ComponentVariable
                {
                    Name = "ConnectorType",
                    Value = "Type2",
                    AttributeType = VariableAttributeType.Actual,
                    Mutability = VariableMutability.ReadOnly,
                },
            ],
        };
        _context.AddRange(station, connector, component);
        await _context.SaveChangesAsync();

        var request = new StatusNotificationRequest
        {
            EvseId = 2,
            ConnectorId = 1,
            ConnectorStatus = ConnectorStatusEnum.Available,
            Timestamp = new DateTime(2026, 11, 14, 10, 30, 0, DateTimeKind.Utc),
        };
        var call = Call(OcppActionNames.StatusNotification, request);
        var result = new RpcResult
        {
            Result = new OcppCallResult("msg-1", "{}"),
            ReturnToCS = true,
        };

        await _publisherService.PublishForCallAsync(call, ChargerId, result, CompletionTime);
        await _publisherService.PublishForCallAsync(call, ChargerId, result, CompletionTime);

        Assert.Equal(2, _publisher.Events.Count);
        Assert.All(_publisher.Events, wrapper =>
        {
            Assert.Equal(WebhookEventTypes.ConnectorStatus, wrapper.EventType);
            var payload = StatusNotificationEvent.Parser.ParseFrom(wrapper.EventData);
            Assert.Equal(ChargerId, payload.ChargerId);
            Assert.Equal(2, payload.EvseId);
            Assert.Equal(1, payload.ConnectorId);
            Assert.Equal("Available", payload.ConnectorStatus);
            Assert.Equal(
                new DateTime(2026, 11, 14, 10, 30, 0, DateTimeKind.Utc),
                payload.StatusTimestamp.ToDateTime());
            Assert.True(payload.HasConnectorType);
            Assert.Equal("Type2", payload.ConnectorType);
        });
    }

    [Fact]
    public async Task NotPublishReplayedTransactionEvents()
    {
        var result = new RpcResult
        {
            Result = new OcppCallResult(
                "msg-1",
                OcppPayloadSerializer.SerializePayload(new TransactionEventResponse())),
            ReturnToCS = true,
            ProcessingContext = new TransactionEventProcessingContext(WasNewlyProcessed: false),
        };

        await _publisherService.PublishForCallAsync(
            Call(OcppActionNames.TransactionEvent, Transaction(TransactionEventEnum.Started)),
            ChargerId,
            result,
            CompletionTime);

        Assert.Empty(_publisher.Events);
    }

    [Fact]
    public async Task NotPublishUpdatedTransactionEvents()
    {
        var call = Call(OcppActionNames.TransactionEvent, Transaction(TransactionEventEnum.Updated));
        var result = TransactionResult();

        await _publisherService.PublishForCallAsync(call, ChargerId, result, CompletionTime);

        Assert.Empty(_publisher.Events);
    }

    [Fact]
    public async Task PublishStartedAndEndedTransactionsWithOptionalConsumption()
    {
        _consumption
            .Setup(s => s.TryComputeConsumption(
                It.IsAny<IAsyncEnumerable<TransactionSample>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(12.5m);

        foreach (var eventType in new[] { TransactionEventEnum.Started, TransactionEventEnum.Ended })
        {
            await _publisherService.PublishForCallAsync(
                Call(OcppActionNames.TransactionEvent, Transaction(eventType)),
                ChargerId,
                TransactionResult(),
                CompletionTime);
        }

        Assert.Collection(
            _publisher.Events,
            wrapper =>
            {
                Assert.Equal(WebhookEventTypes.TransactionStarted, wrapper.EventType);
                var payload = TransactionLifecycleEvent.Parser.ParseFrom(wrapper.EventData);
                Assert.Equal(ChargerId, payload.ChargerId);
                Assert.Equal("transaction-1", payload.TransactionId);
                Assert.Equal(12.5, payload.ConsumptionWh);
            },
            wrapper =>
            {
                Assert.Equal(WebhookEventTypes.TransactionEnded, wrapper.EventType);
                var payload = TransactionLifecycleEvent.Parser.ParseFrom(wrapper.EventData);
                Assert.Equal("transaction-1", payload.TransactionId);
                Assert.True(payload.HasConsumptionWh);
                Assert.Equal(12.5, payload.ConsumptionWh);
            });
    }

    [Fact]
    public async Task PublishWithoutOptionalConsumptionWhenEnrichmentTimesOut()
    {
        _consumption
            .Setup(s => s.TryComputeConsumption(
                It.IsAny<IAsyncEnumerable<TransactionSample>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (IAsyncEnumerable<TransactionSample> _, CancellationToken cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return (decimal?)null;
            });

        var started = Stopwatch.GetTimestamp();
        await _publisherService.PublishForCallAsync(
            Call(OcppActionNames.TransactionEvent, Transaction(TransactionEventEnum.Started)),
            ChargerId,
            TransactionResult(),
            CompletionTime);
        var elapsed = Stopwatch.GetElapsedTime(started);

        Assert.True(elapsed < TimeSpan.FromSeconds(3), $"Enrichment was not bounded: {elapsed}.");
        var payload = TransactionLifecycleEvent.Parser.ParseFrom(Assert.Single(_publisher.Events).EventData);
        Assert.False(payload.HasConsumptionWh);
    }

    [Fact]
    public async Task PreserveAbsentConsumptionWhenItCannotBeComputed()
    {
        _consumption
            .Setup(s => s.TryComputeConsumption(
                It.IsAny<IAsyncEnumerable<TransactionSample>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal?)null);

        await _publisherService.PublishForCallAsync(
            Call(OcppActionNames.TransactionEvent, Transaction(TransactionEventEnum.Started)),
            ChargerId,
            TransactionResult(),
            CompletionTime);

        var payload = TransactionLifecycleEvent.Parser.ParseFrom(Assert.Single(_publisher.Events).EventData);
        Assert.False(payload.HasConsumptionWh);
    }

    public void Dispose() => _context.Dispose();

    private static OcppCallRequest Call<T>(string action, T payload) where T : class =>
        new("msg-1", action, OcppPayloadSerializer.SerializePayload(payload));

    private static RpcResult Result(BootNotificationResponse response) => new()
    {
        Result = new OcppCallResult("msg-1", OcppPayloadSerializer.SerializePayload(response)),
        ReturnToCS = true,
    };

    private static RpcResult TransactionResult() => new()
    {
        Result = new OcppCallResult("msg-1", "{}"),
        ReturnToCS = true,
        ProcessingContext = new TransactionEventProcessingContext(WasNewlyProcessed: true),
    };

    private static TransactionEventRequest Transaction(TransactionEventEnum eventType) => new()
    {
        EventType = eventType,
        Timestamp = new DateTime(2026, 11, 14, 10, 30, 0, DateTimeKind.Utc),
        TriggerReason = TriggerReasonEnum.Authorized,
        SeqNo = 0,
        TransactionInfo = new Transaction { TransactionId = "transaction-1" },
    };

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

    private sealed class TestDbContext(DbContextOptions<MedinillaOcppDbContext> options)
        : MedinillaOcppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<AuthorizationDetails>().Ignore(details => details.AuthBlob);
        }
    }
}
