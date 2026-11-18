using Medinilla.Core.Events;
using Medinilla.Core.Logic.Configuration;
using Medinilla.Core.SharedContracts.Events;
using Medinilla.Core.v1.Services;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using DbChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;
using OcppChargingStation = Medinilla.DataTypes.Contracts.Common.ChargingStation;

namespace Medinilla.Core.Tests;

public class ChargingStationBootingShould
{
    [Theory]
    [InlineData(null)]
    [InlineData("Voltage")]
    [InlineData("")]
    public async Task InitializeMissingAccountSettingsFromDefaultsWithoutOverwritingExistingSettings(
        string? allowedMeasurands)
    {
        var options = new DbContextOptionsBuilder<MedinillaOcppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new TestDbContext(options);
        var account = new Account { Id = Guid.NewGuid(), Name = "MedinillaTest-Core" };
        if (allowedMeasurands is not null)
        {
            account.Settings = new AccountSettings
            {
                AccountId = account.Id,
                Account = account,
                AllowedMeasurands = allowedMeasurands,
            };
        }

        context.Add(account);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, new FakeApplicationEventEmitter());
        var request = CreateRequest();

        var result = await service.ProcessBootup("station-1", request);

        Assert.Equal(BootupResult.FirstBoot, result);
        context.ChangeTracker.Clear();
        var settings = await context.Set<AccountSettings>().SingleAsync();
        Assert.Equal(account.Id, settings.AccountId);
        var defaults = CentralConfig.GetMedinillaConfiguration().DefaultAllowedMeasurands;
        Assert.False(string.IsNullOrWhiteSpace(defaults));
        Assert.Equal(allowedMeasurands ?? defaults, settings.AllowedMeasurands);

        // A second station on the account must reuse the same settings.
        await service.ProcessBootup("station-2", request);
        Assert.Equal(settings, await context.Set<AccountSettings>().SingleAsync());
        Assert.Equal(allowedMeasurands ?? defaults, settings.AllowedMeasurands);
    }

    [Fact]
    public async Task EmitBootupAfterACommittedFirstBoot()
    {
        var options = new DbContextOptionsBuilder<MedinillaOcppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new TestDbContext(options);
        var account = new Account { Id = Guid.NewGuid(), Name = "MedinillaTest-Core" };
        context.Add(account);
        await context.SaveChangesAsync();

        var emitter = new FakeApplicationEventEmitter();
        var service = CreateService(context, emitter);

        await service.ProcessBootup("station-1", CreateRequest());

        var emitted = Assert.Single(emitter.Events);
        Assert.Equal(WebhookEventTypes.ChargerBootup, emitted.EventType);
        var payload = Assert.IsType<BootupEvent>(emitted.Payload);
        Assert.Equal("station-1", payload.ChargerId);
        Assert.Equal(account.Id.ToString("D"), payload.AccountId);
        Assert.Equal("PowerUp", payload.BootReason);
        Assert.Equal("Vendor", payload.VendorName);
        Assert.Equal("Model", payload.Model);
    }

    [Fact]
    public async Task NotEmitBootupWhenPersistenceFails()
    {
        var options = new DbContextOptionsBuilder<MedinillaOcppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new FailingDbContext(options);
        context.Add(new DbChargingStation
        {
            Id = Guid.NewGuid(),
            ClientIdentifier = "station-1",
            Model = "Model",
            Vendor = "Vendor",
            LatestBootNotificationReason = "PowerUp",
        });
        await context.SaveChangesAsync();
        context.FailSaves = true;

        var emitter = new FakeApplicationEventEmitter();
        var service = CreateService(context, emitter);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ProcessBootup("station-1", CreateRequest()));

        Assert.Empty(emitter.Events);
    }

    private static ChargingStationBooting CreateService(
        MedinillaOcppDbContext context,
        FakeApplicationEventEmitter emitter) => new(
        context,
        emitter,
        new BootupEventMapper(),
        NullLogger<ChargingStationBooting>.Instance);

    private static BootNotificationRequest CreateRequest() => new(
        BootReasonEnum.PowerUp,
        new OcppChargingStation
        {
            Model = "Model",
            VendorName = "Vendor",
        });

    private sealed class TestDbContext(DbContextOptions<MedinillaOcppDbContext> options)
        : MedinillaOcppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // PostgreSQL JSON storage is unsupported by the InMemory provider.
            modelBuilder.Entity<AuthorizationDetails>().Ignore(details => details.AuthBlob);
        }
    }

    private sealed class FailingDbContext(DbContextOptions<MedinillaOcppDbContext> options)
        : MedinillaOcppDbContext(options)
    {
        public bool FailSaves { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            FailSaves
                ? throw new InvalidOperationException("Simulated persistence failure")
                : base.SaveChangesAsync(cancellationToken);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<AuthorizationDetails>().Ignore(details => details.AuthBlob);
        }
    }
}
