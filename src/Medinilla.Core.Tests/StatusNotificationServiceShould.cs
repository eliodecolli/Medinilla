using Medinilla.Core.Events;
using Medinilla.Core.SharedContracts.Events;
using Medinilla.Core.v1.Services;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using DbChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;

namespace Medinilla.Core.Tests;

public class StatusNotificationServiceShould : IDisposable
{
    private static readonly DateTime ReportedAt =
        new(2026, 11, 14, 10, 30, 0, DateTimeKind.Utc);

    private readonly TestDbContext _context;
    private readonly FakeApplicationEventEmitter _emitter = new();

    public StatusNotificationServiceShould()
    {
        var options = new DbContextOptionsBuilder<MedinillaOcppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new TestDbContext(options);
        _context.Add(new DbChargingStation
        {
            Id = Guid.NewGuid(),
            ClientIdentifier = "station-1",
            Model = "Model",
            Vendor = "Vendor",
            LatestBootNotificationReason = "PowerUp",
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task EmitEveryCommittedStatusNotification()
    {
        var service = new StatusNotificationService(
            _context,
            _emitter,
            new StatusNotificationEventMapper(),
            NullLogger<StatusNotificationService>.Instance);
        var request = CreateRequest();

        await service.ProcessNotification("station-1", request);
        await service.ProcessNotification("station-1", request);

        Assert.Equal(2, _emitter.Events.Count);
        Assert.All(_emitter.Events, emitted =>
        {
            Assert.Equal(WebhookEventTypes.ConnectorStatus, emitted.EventType);
            var payload = Assert.IsType<StatusNotificationEvent>(emitted.Payload);
            Assert.Equal("station-1", payload.ChargerId);
            Assert.Equal(2, payload.EvseId);
            Assert.Equal(1, payload.ConnectorId);
            Assert.Equal("Available", payload.ConnectorStatus);
            Assert.Equal(ReportedAt, payload.StatusTimestamp.ToDateTime());
        });
        Assert.Single(_context.Set<EvseConnector>());
    }

    public void Dispose() => _context.Dispose();

    private static StatusNotificationRequest CreateRequest() => new()
    {
        EvseId = 2,
        ConnectorId = 1,
        ConnectorStatus = ConnectorStatusEnum.Available,
        Timestamp = ReportedAt,
    };

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
