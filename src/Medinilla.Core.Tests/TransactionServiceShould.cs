using System.Globalization;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.Logic.Authorization;
using Medinilla.Core.Logic.Authorization.Algorithms;
using Medinilla.Core.v1.Services;
using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.Enums;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure.Core.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;
using DbIdToken = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;
using OcppIdToken = Medinilla.DataTypes.Contracts.Common.IdToken;

namespace Medinilla.Core.Tests;

public class TransactionServiceShould : IDisposable
{
    private const string ClientIdentifier = "station-1";
    private static readonly DateTime EventTimestamp = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestDbContext _context;
    private readonly Mock<IAuthAlgorithm> _authorization = new(MockBehavior.Strict);
    private readonly TransactionService _service;

    public TransactionServiceShould()
    {
        var options = new DbContextOptionsBuilder<MedinillaOcppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new TestDbContext(options);
        _authorization.SetupGet(a => a.Algorithm).Returns(AuthorizationAlgorithm.Default);
        _authorization.SetupGet(a => a.Priority).Returns(0);
        _authorization.Setup(a => a.Authorize(It.IsAny<AuthorizationContext>()))
            .ReturnsAsync(AuthorizeStatus.Accepted);
        _service = CreateService(_authorization.Object);
    }

    [Fact]
    public async Task ThrowWhenTheChargingStationDoesNotExist()
    {
        await Assert.ThrowsAsync<AggregateRootNotFoundException>(
            () => _service.IngestEvent(ClientIdentifier, CreateRequest()));

        _authorization.Verify(a => a.Authorize(It.IsAny<AuthorizationContext>()), Times.Never);
        Assert.Empty(_context.ChangeTracker.Entries<TransactionEvent>());
    }

    [Fact]
    public async Task IgnoreAnExistingTransactionAndSequenceBeforeAuthorization()
    {
        var station = await AddStation();
        var existing = new TransactionEvent { TransactionId = "transaction-1", SeqNo = 0 };
        station.TransactionEvents.Add(existing);

        var response = await _service.IngestEvent(ClientIdentifier, CreateRequest());

        Assert.Same(existing, Assert.Single(station.TransactionEvents));
        Assert.Null(response.IdTokenInfo);
        _authorization.Verify(a => a.Authorize(It.IsAny<AuthorizationContext>()), Times.Never);
    }

    [Theory]
    [InlineData("another-transaction", 0)]
    [InlineData("transaction-1", 1)]
    public async Task DistinguishEventsByBothTransactionIdAndSequence(string transactionId, int seqNo)
    {
        var station = await AddStation();
        station.TransactionEvents.Add(new TransactionEvent { TransactionId = "transaction-1", SeqNo = 0 });
        var request = CreateRequest();
        request.TransactionInfo.TransactionId = transactionId;
        request.SeqNo = seqNo;

        await _service.IngestEvent(ClientIdentifier, request);

        Assert.Equal(2, station.TransactionEvents.Count);
        Assert.Contains(station.TransactionEvents, e => e.TransactionId == transactionId && e.SeqNo == seqNo);
        _authorization.Verify(a => a.Authorize(It.IsAny<AuthorizationContext>()), Times.Once);
    }

    [Fact]
    public async Task MapEventFieldsAndAssociateTheEventWithItsChargingStation()
    {
        var station = await AddStation();
        var request = CreateRequest();
        request.EventType = TransactionEventEnum.Ended;
        request.TriggerReason = TriggerReasonEnum.RemoteStop;
        request.Timestamp = EventTimestamp.ToLocalTime();
        request.SeqNo = 7;
        request.Offline = true;
        request.NumberOfPhasesUsed = 3;
        request.CableMaxCurrent = 32;
        request.ReservationId = 12;
        request.Evse = new EVSE { Id = 2, ConnectorId = 1 };
        request.TransactionInfo.ChargingState = ChargingStateEnum.SuspendedEVSE;
        request.TransactionInfo.TimeSpentCharging = 600;
        request.TransactionInfo.StoppedReason = ReasonEnum.Remote;
        request.TransactionInfo.RemoteStartId = 42;

        await _service.IngestEvent(ClientIdentifier, request);
        _context.ChangeTracker.DetectChanges();

        var stored = Assert.Single(station.TransactionEvents);
        Assert.Equal("transaction-1", stored.TransactionId);
        Assert.Equal(TransactionEventType.Ended, stored.EventType);
        Assert.Equal(TransactionTriggerReason.RemoteStop, stored.TriggerReason);
        Assert.Equal(EventTimestamp, stored.Timestamp);
        Assert.Equal(DateTimeKind.Utc, stored.Timestamp.Kind);
        Assert.Equal(7, stored.SeqNo);
        Assert.Equal(true, stored.Offline);
        Assert.Equal(3, stored.NumberOfPhasesUsed);
        Assert.Equal(32, stored.CableMaxCurrent);
        Assert.Equal(12, stored.ReservationId);
        Assert.Equal(ChargingState.SuspendedEVSE, stored.ChargingState);
        Assert.Equal(600, stored.TimeSpentCharging);
        Assert.Equal(TransactionStoppedReason.Remote, stored.StoppedReason);
        Assert.Equal(42, stored.RemoteStartId);
        Assert.Equal(2, stored.EvseId);
        Assert.Equal(1, stored.ConnectorId);
        Assert.Same(station, stored.ChargingStation);
        Assert.Equal(station.Id, stored.ChargingStationId);
    }

    [Fact]
    public async Task PreserveAbsentOptionalEventFieldsAndHandleMissingMeterValues()
    {
        var station = await AddStation();

        var response = await _service.IngestEvent(ClientIdentifier, CreateRequest());

        var stored = Assert.Single(station.TransactionEvents);
        Assert.Equal(TransactionEventType.Started, stored.EventType);
        Assert.Equal(TransactionTriggerReason.Authorized, stored.TriggerReason);
        Assert.Null(stored.Offline);
        Assert.Null(stored.NumberOfPhasesUsed);
        Assert.Null(stored.CableMaxCurrent);
        Assert.Null(stored.ReservationId);
        Assert.Null(stored.ChargingState);
        Assert.Null(stored.TimeSpentCharging);
        Assert.Null(stored.StoppedReason);
        Assert.Null(stored.RemoteStartId);
        Assert.Null(stored.EvseId);
        Assert.Null(stored.ConnectorId);
        Assert.Null(stored.IdToken);
        Assert.Empty(stored.Samples);
        Assert.Null(response.IdTokenInfo);
    }

    [Fact]
    public async Task AuthorizeAndAttachTheExistingRequestToken()
    {
        var station = await AddStation();
        station.Location = "garage";
        var token = new DbIdToken { Token = "token-1", IdType = "ISO14443" };
        station.IdTokens.Add(token);
        var request = CreateRequest();
        request.IdToken = new OcppIdToken { Token = token.Token, Type = IdTokenType.ISO14443 };
        request.Evse = new EVSE { Id = 2 };

        var response = await _service.IngestEvent(ClientIdentifier, request);

        Assert.Same(token, Assert.Single(station.TransactionEvents).IdToken);
        Assert.Equal(AuthorizeStatus.Accepted, response.IdTokenInfo?.Status);
        Assert.Single(station.IdTokens);
        _authorization.Verify(a => a.Authorize(It.Is<AuthorizationContext>(c =>
            c.IdToken == token && c.EvseId == 2 && c.LocationName == "garage")), Times.Once);
    }

    [Fact]
    public async Task RecoverTheTokenFromAnEarlierEventOfTheSameTransaction()
    {
        var station = await AddStation();
        var unrelatedToken = new DbIdToken { Token = "other-token", IdType = "ISO14443" };
        var token = new DbIdToken { Token = "token-1", IdType = "ISO14443" };
        station.TransactionEvents.Add(new TransactionEvent
        {
            TransactionId = "other-transaction", SeqNo = 0, IdToken = unrelatedToken,
        });
        station.TransactionEvents.Add(new TransactionEvent
        {
            TransactionId = "transaction-1", SeqNo = 0, IdToken = token,
        });
        var request = CreateRequest();
        request.EventType = TransactionEventEnum.Updated;
        request.SeqNo = 1;

        var response = await _service.IngestEvent(ClientIdentifier, request);

        var stored = Assert.Single(station.TransactionEvents.Where(e =>
            e.TransactionId == "transaction-1" && e.SeqNo == 1));
        Assert.Same(token, stored.IdToken);
        Assert.Null(response.IdTokenInfo);
        _authorization.Verify(a => a.Authorize(It.Is<AuthorizationContext>(c => c.IdToken == token)), Times.Once);
    }

    [Theory]
    [InlineData(AuthorizeStatus.Invalid)]
    [InlineData(AuthorizeStatus.Blocked)]
    [InlineData(AuthorizeStatus.Expired)]
    public async Task ReturnAuthorizationFailureWithoutAppendingAnEvent(string status)
    {
        var station = await AddStation();
        _authorization.Setup(a => a.Authorize(It.IsAny<AuthorizationContext>())).ReturnsAsync(status);
        var request = CreateRequest();
        request.MeterValue = [CreateMeter(EventTimestamp, new SampledValue { Value = 10m })];

        var response = await _service.IngestEvent(ClientIdentifier, request);

        Assert.Equal(status, response.IdTokenInfo?.Status);
        Assert.Empty(station.TransactionEvents);
        Assert.Empty(_context.ChangeTracker.Entries<TransactionEvent>());
        Assert.Empty(_context.ChangeTracker.Entries<TransactionSample>());
    }

    [Fact]
    public async Task RejectAnUnknownRequestTokenUsingDefaultAuthorization()
    {
        var station = await AddStation();
        var request = CreateRequest();
        request.IdToken = new OcppIdToken { Token = "unknown", Type = IdTokenType.ISO14443 };

        var response = await CreateService(new DefaultAuthorization()).IngestEvent(ClientIdentifier, request);

        Assert.Equal(AuthorizeStatus.Invalid, response.IdTokenInfo?.Status);
        Assert.Empty(station.IdTokens);
        Assert.Empty(station.TransactionEvents);
    }

    [Fact]
    public async Task FlattenAllSamplesAndPreserveEachParentMeterTimestamp()
    {
        var station = await AddStation();
        var firstTimestamp = EventTimestamp.AddMinutes(-2);
        var secondTimestamp = EventTimestamp.AddMinutes(-1);
        var request = CreateRequest();
        request.MeterValue =
        [
            CreateMeter(firstTimestamp.ToLocalTime(),
                new SampledValue { Value = 100m },
                new SampledValue
                {
                    Value = 230m, Measurand = MeasurandEnum.Voltage,
                    Location = LocationEnum.Inlet, UnitOfMeasure = new UnitOfMeasure { Unit = "V" },
                    Context = ReadingContextEnum.SampleClock, Phase = PhaseEnum.L1N,
                }),
            CreateMeter(secondTimestamp, new SampledValue
            {
                Value = 200m, Measurand = MeasurandEnum.EnergyActiveExportRegister,
                Location = LocationEnum.EV,
                Context = ReadingContextEnum.TransactionEnd, Phase = PhaseEnum.L3,
            }),
        ];

        await _service.IngestEvent(ClientIdentifier, request);
        _context.ChangeTracker.DetectChanges();

        var stored = Assert.Single(station.TransactionEvents);
        Assert.Collection(stored.Samples,
            sample =>
            {
                Assert.Equal(firstTimestamp, sample.Timestamp);
                Assert.Equal(100m, sample.Value);
                Assert.Equal(Measurand.EnergyActiveImportRegister, sample.Measurand);
                Assert.Equal(MeasurementLocation.Outlet, sample.Location);
                Assert.Equal(ReadingContext.SamplePeriodic, sample.Context);
                Assert.Null(sample.Phase);
            },
            sample =>
            {
                Assert.Equal(firstTimestamp, sample.Timestamp);
                Assert.Equal(230m, sample.Value);
                Assert.Equal(Measurand.Voltage, sample.Measurand);
                Assert.Equal(MeasurementLocation.Inlet, sample.Location);
                Assert.Equal(ReadingContext.SampleClock, sample.Context);
                Assert.Equal(MeasurementPhase.L1N, sample.Phase);
            },
            sample =>
            {
                Assert.Equal(secondTimestamp, sample.Timestamp);
                Assert.Equal(200m, sample.Value);
                Assert.Equal(Measurand.EnergyActiveExportRegister, sample.Measurand);
                Assert.Equal(MeasurementLocation.EV, sample.Location);
                Assert.Equal(ReadingContext.TransactionEnd, sample.Context);
                Assert.Equal(MeasurementPhase.L3, sample.Phase);
            });
        Assert.All(stored.Samples, sample =>
        {
            Assert.Equal(DateTimeKind.Utc, sample.Timestamp.Kind);
            Assert.NotEqual(stored.Timestamp, sample.Timestamp);
            Assert.Same(stored, sample.TransactionEvent);
            Assert.Equal(stored.Id, sample.TransactionEventId);
        });
    }

    [Theory]
    [InlineData(null, ReadingContext.SamplePeriodic)]
    [InlineData(ReadingContextEnum.InterruptionBegin, ReadingContext.InterruptionBegin)]
    [InlineData(ReadingContextEnum.InterruptionEnd, ReadingContext.InterruptionEnd)]
    [InlineData(ReadingContextEnum.Other, ReadingContext.Other)]
    [InlineData(ReadingContextEnum.SampleClock, ReadingContext.SampleClock)]
    [InlineData(ReadingContextEnum.SamplePeriodic, ReadingContext.SamplePeriodic)]
    [InlineData(ReadingContextEnum.TransactionBegin, ReadingContext.TransactionBegin)]
    [InlineData(ReadingContextEnum.TransactionEnd, ReadingContext.TransactionEnd)]
    [InlineData(ReadingContextEnum.Trigger, ReadingContext.Trigger)]
    public async Task MapAndPersistReadingContext(ReadingContextEnum? context, ReadingContext expected)
    {
        await AddStation();
        var request = CreateRequest();
        request.MeterValue = [CreateMeter(EventTimestamp, new SampledValue { Value = 10m, Context = context })];

        await _service.IngestEvent(ClientIdentifier, request);

        var stored = await _context.Set<TransactionSample>().AsNoTracking().SingleAsync();
        Assert.Equal(expected, stored.Context);
    }

    [Fact]
    public async Task StoreOnlyTheSamplesWhoseMeasurandIsAllowed()
    {
        var station = await AddStation($"{nameof(Measurand.Voltage)},{nameof(Measurand.EnergyActiveImportRegister)}");
        var request = CreateRequest();
        request.MeterValue = [CreateMeter(EventTimestamp,
            new SampledValue { Value = 230m, Measurand = MeasurandEnum.Voltage },
            new SampledValue { Value = 200m, Measurand = MeasurandEnum.EnergyActiveExportRegister },
            new SampledValue { Value = 100m })];

        await _service.IngestEvent(ClientIdentifier, request);

        var stored = Assert.Single(station.TransactionEvents).Samples;
        Assert.Collection(stored,
            sample =>
            {
                Assert.Equal(230m, sample.Value);
                Assert.Equal(Measurand.Voltage, sample.Measurand);
            },
            sample =>
            {
                Assert.Equal(100m, sample.Value);
                Assert.Equal(Measurand.EnergyActiveImportRegister, sample.Measurand);
            });
    }

    [Fact]
    public async Task StoreAllSamplesWhenNoMeasurandIsConfigured()
    {
        var station = await AddStation(string.Empty);
        var request = CreateRequest();
        request.MeterValue = [CreateMeter(EventTimestamp,
            new SampledValue { Value = 230m, Measurand = MeasurandEnum.Voltage })];

        await _service.IngestEvent(ClientIdentifier, request);

        var stored = Assert.Single(station.TransactionEvents);
        Assert.Equal(Measurand.Voltage, Assert.Single(stored.Samples).Measurand);
        Assert.Single(_context.ChangeTracker.Entries<TransactionSample>());
    }

    [Fact]
    public async Task IgnoreUnknownMeasurandsInTheAllowedMeasurandsSettings()
    {
        var station = await AddStation($"NotAMeasurand, {nameof(Measurand.Voltage)}");
        var request = CreateRequest();
        request.MeterValue = [CreateMeter(EventTimestamp,
            new SampledValue { Value = 230m, Measurand = MeasurandEnum.Voltage },
            new SampledValue { Value = 200m, Measurand = MeasurandEnum.EnergyActiveExportRegister })];

        await _service.IngestEvent(ClientIdentifier, request);

        var stored = Assert.Single(Assert.Single(station.TransactionEvents).Samples);
        Assert.Equal(Measurand.Voltage, stored.Measurand);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(PhaseEnum.L1, MeasurementPhase.L1)]
    [InlineData(PhaseEnum.L2, MeasurementPhase.L2)]
    [InlineData(PhaseEnum.L3, MeasurementPhase.L3)]
    [InlineData(PhaseEnum.N, MeasurementPhase.N)]
    [InlineData(PhaseEnum.L1N, MeasurementPhase.L1N)]
    [InlineData(PhaseEnum.L2N, MeasurementPhase.L2N)]
    [InlineData(PhaseEnum.L3N, MeasurementPhase.L3N)]
    [InlineData(PhaseEnum.L1L2, MeasurementPhase.L1L2)]
    [InlineData(PhaseEnum.L2L3, MeasurementPhase.L2L3)]
    [InlineData(PhaseEnum.L3L1, MeasurementPhase.L3L1)]
    public async Task MapAndPersistPhase(PhaseEnum? phase, MeasurementPhase? expected)
    {
        await AddStation();
        var request = CreateRequest();
        request.MeterValue = [CreateMeter(EventTimestamp, new SampledValue { Value = 10m, Phase = phase })];

        await _service.IngestEvent(ClientIdentifier, request);

        var stored = await _context.Set<TransactionSample>().AsNoTracking().SingleAsync();
        Assert.Equal(expected, stored.Phase);
    }

    [Theory]
    [InlineData(null, 0, "1.5", "1.5")]
    [InlineData("Wh", 0, "1.5", "1.5")]
    [InlineData("Wh", 3, "1.5", "1500")]
    [InlineData("Wh", -3, "1500", "1.5")]
    [InlineData("kWh", 0, "1.5", "1500")]
    [InlineData("kWh", 3, "1.5", "1500000")]
    [InlineData("kWh", -3, "1.5", "1.5")]
    [InlineData("Wh", 0, "0", "0")]
    public async Task NormalizeEnergyToWh(string? unit, int multiplier, string value, string expected)
    {
        var station = await AddStation();
        var request = CreateRequest();
        request.MeterValue = [CreateMeter(EventTimestamp, new SampledValue
        {
            Value = decimal.Parse(value, CultureInfo.InvariantCulture),
            UnitOfMeasure = unit is null ? null! : new UnitOfMeasure { Unit = unit, Multiplier = multiplier },
        })];

        await _service.IngestEvent(ClientIdentifier, request);

        var sample = Assert.Single(Assert.Single(station.TransactionEvents).Samples);
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), sample.Value);
    }

    [Fact]
    public async Task AvoidAppendingTheSameEventAndSamplesTwice()
    {
        var station = await AddStation();
        var request = CreateRequest();
        request.MeterValue = [CreateMeter(EventTimestamp, new SampledValue { Value = 10m })];

        await _service.IngestEvent(ClientIdentifier, request);
        await _service.IngestEvent(ClientIdentifier, request);

        Assert.Single(Assert.Single(station.TransactionEvents).Samples);
        _authorization.Verify(a => a.Authorize(It.IsAny<AuthorizationContext>()), Times.Once);
    }

    private TransactionService CreateService(params IAuthAlgorithm[] algorithms) => new(
        NullLogger<ITransactionService>.Instance,
        _context,
        new AuthorizationAlgorithmFactory(algorithms, NullLogger<AuthorizationAlgorithmFactory>.Instance));

    private async Task<ChargingStation> AddStation(string? allowedMeasurands = null)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Test account",
            Settings = new AccountSettings
            {
                AccountId = Guid.NewGuid(),
                AllowedMeasurands = allowedMeasurands ?? string.Join(",", Enum.GetNames<Measurand>()),
                Account = null!,
            },
        };
        account.Settings.Account = account;

        var station = new ChargingStation
        {
            Id = Guid.NewGuid(),
            ClientIdentifier = ClientIdentifier,
            Model = "Test model",
            Vendor = "Test vendor",
            LatestBootNotificationReason = "PowerUp",
            AccountId = account.Id,
            Account = account,
        };
        _context.Add(station);
        await _context.SaveChangesAsync();
        return station;
    }

    private static TransactionEventRequest CreateRequest() => new()
    {
        EventType = TransactionEventEnum.Started,
        Timestamp = EventTimestamp,
        TriggerReason = TriggerReasonEnum.Authorized,
        SeqNo = 0,
        TransactionInfo = new Transaction { TransactionId = "transaction-1" },
    };

    private static MeterValue CreateMeter(DateTime timestamp, params SampledValue[] samples) => new()
    {
        Timestamp = timestamp,
        SampledValue = samples.ToList(),
    };

    public void Dispose() => _context.Dispose();

    private sealed class TestDbContext(DbContextOptions<MedinillaOcppDbContext> options)
        : MedinillaOcppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // PostgreSQL JSON storage is unrelated to these ingestion tests and unsupported by InMemory.
            modelBuilder.Entity<AuthorizationDetails>().Ignore(details => details.AuthBlob);
        }
    }
}
