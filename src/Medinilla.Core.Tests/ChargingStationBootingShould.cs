using Medinilla.Core.Logic.Configuration;
using Medinilla.Core.v1.Services;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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

        var service = new ChargingStationBooting(context, NullLogger<ChargingStationBooting>.Instance);
        var request = new BootNotificationRequest(BootReasonEnum.PowerUp, new OcppChargingStation
        {
            Model = "Test model",
            VendorName = "Test vendor",
        });

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
}
