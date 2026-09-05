using Medinilla.Core.Interfaces;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.Logic.Configuration;
using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Core.Enums;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Medinilla.DataAccess.Relational;
using Microsoft.EntityFrameworkCore;

namespace Medinilla.Core.v1.Services;

public class ChargingStationBooting(MedinillaOcppDbContext context, ILogger<ChargingStationBooting> log) : IChargingStationBootingService
{
    private string GetBootupReason(BootNotificationRequest request)
    {
        return Enum.GetName(request.Reason) ?? "UnkownReason";
    }
    
    private ChargingStation GetChargingStation(string clientIdentifier, BootNotificationRequest request)
    {
        return new ChargingStation()
        {
            ClientIdentifier = clientIdentifier,
            Model = request.ChargingStation.Model,
            Vendor = request.ChargingStation.VendorName,
            LatestBootNotificationReason = GetBootupReason(request),
        };
    }

    private async Task TryBootstrapChargingStatation(ChargingStation chargingStation)
    {
        var medinillaSettings = CentralConfig.GetMedinillaConfiguration();

        if (chargingStation.Tariffs.Count == 0)
        {
            // get default unit price
            var defaultUnit = medinillaSettings.DefaultUnit;

            chargingStation.Tariffs.Add(new Tariff()
            {
                Id = Guid.NewGuid(),
                UnitName = defaultUnit.Name,
                UnitPrice = (decimal)defaultUnit.Price,
            });
        }

        if (chargingStation.AuthorizationDetails is null)
        {
            chargingStation.AuthorizationDetails = new AuthorizationDetails()
            {
                AuthBlob = JsonDocument.Parse(medinillaSettings.DefaultAuthDetails ?? "{}"),
            };
        }

        if (chargingStation.IdTokens.Count == 0 && medinillaSettings.UseDefaultUser)
        {
            var defaultUser = medinillaSettings.DefaultUser;
            var entityUser = new AuthorizationUser()
            {
                ActiveCredit = (decimal)defaultUser.ActiveCredit,
                DisplayName = defaultUser.DisplayName,
                IsActive = true
            };
            chargingStation.AuthorizationUsers.Add(entityUser);

            chargingStation.IdTokens.Add(new IdToken()
            {
                User = entityUser,
                Token = defaultUser.Token,
                CreatedDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(100000),
                IdType = "ISO14443"
            });
        }
    }

    public async Task<BootupResult> ProcessBootup(string clientIdentifier, BootNotificationRequest request)
    {
        var bootStatus = BootupResult.Ok;
        try
        {
            var chargingStation = await context.GetChargingStation(clientIdentifier);
            chargingStation.LatestBootNotificationReason = GetBootupReason(request);
            chargingStation.Booted = true;

            if (chargingStation.ModifiedAt is null)
            {
                bootStatus = BootupResult.FirstBoot;
            }

            chargingStation.ModifiedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
        catch (AggregateRootNotFoundException)
        {
            var entity = GetChargingStation(clientIdentifier, request);
            await context.Set<ChargingStation>().AddAsync(entity);
            
            var account = await context.Set<Account>()
                .Where(c => c.Name == "MedinillaTest-Core")
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            
            // let account throw here agin if it's not found
            // this logic will be replaced in https://github.com/eliodecolli/Medinilla/issues/24
            entity.AccountId = account!.Id;
            entity.CreatedAt = DateTime.UtcNow;
            entity.LatestBootNotificationReason = GetBootupReason(request);
            entity.Booted = true;

            await TryBootstrapChargingStatation(entity);
            await context.SaveChangesAsync();

            bootStatus = BootupResult.FirstBoot;
        }
        
        return bootStatus;
    }

    public async Task DisconnectClient(string clientIdentifier)
    {
        var chargingStation = await context.GetChargingStation(clientIdentifier);
        chargingStation.Booted = false;
        await context.SaveChangesAsync();
    }
}