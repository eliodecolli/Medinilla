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
using Microsoft.EntityFrameworkCore;

namespace Medinilla.Core.v1.Services;

public class ChargingStationBooting(ChargingStationUnitOfWork unitOfWork, ILogger<ChargingStationBooting> log) : IChargingStationBootingService
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

    private async Task TryBootstrapChargingStatation()
    {
        var medinillaSettings = CentralConfig.GetMedinillaConfiguration();

        if (unitOfWork.AggregateRoot.Tariffs.Count == 0)
        {
            // get default unit price
            var defaultUnit = medinillaSettings.DefaultUnit;

            await unitOfWork.Tariffs.AddAsync(new Tariff()
            {
                Id = Guid.NewGuid(),
                ChargingStationId = unitOfWork.AggregateRoot.Id,
                UnitName = defaultUnit.Name,
                UnitPrice = (decimal)defaultUnit.Price,
            });
        }

        if (unitOfWork.AggregateRoot.AuthorizationDetails is null)
        {
            await unitOfWork.AuthorizationDetails.AddAsync(new AuthorizationDetails()
            {
                AuthBlob = JsonDocument.Parse(medinillaSettings.DefaultAuthDetails ?? "{}"),
                ChargingStationId = unitOfWork.AggregateRoot.Id,
            });
        }

        if (unitOfWork.AggregateRoot.IdTokens.Count == 0 && medinillaSettings.UseDefaultUser)
        {
            var defaultUser = medinillaSettings.DefaultUser;
            var entityUser = (await unitOfWork.AuthorizationUser.AddAsync(new AuthorizationUser()
            {
                ChargingStationId = unitOfWork.AggregateRoot.Id,
                ActiveCredit = (decimal)defaultUser.ActiveCredit,
                DisplayName = defaultUser.DisplayName,
                IsActive = true
            })).Entity;

            await unitOfWork.IdTokens.AddAsync(new IdToken()
            {
                ChargingStationId = unitOfWork.AggregateRoot.Id,
                AuthorizationUserId = entityUser.Id,
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
            await unitOfWork.Start(c => c.ClientIdentifier == clientIdentifier);
        }
        catch (AggregateRootNotFoundException)
        {
            var entity = GetChargingStation(clientIdentifier, request);
            await unitOfWork.Start(entity);
            
            var account = await unitOfWork.Accounts
                .Where(c => c.Name == "MedinillaTest-Core")
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            
            // let account throw here agin if it's not found
            // this logic will be replaced in https://github.com/eliodecolli/Medinilla/issues/24
            entity.AccountId = account!.Id;
            entity.CreatedAt = DateTime.UtcNow;
            entity.LatestBootNotificationReason = GetBootupReason(request);
            entity.Booted = true;

            await unitOfWork.Save();  // gotta trigger a save so the next thing proceeds
            await TryBootstrapChargingStatation();

            bootStatus = BootupResult.FirstBoot;
        }

        unitOfWork.AggregateRoot.LatestBootNotificationReason = GetBootupReason(request);
        unitOfWork.AggregateRoot.Booted = true;

        if (unitOfWork.AggregateRoot.ModifiedAt is null)
        {
            bootStatus = BootupResult.FirstBoot;
        }

        unitOfWork.AggregateRoot.ModifiedAt = DateTime.UtcNow;
        await unitOfWork.Save();
        return bootStatus;
    }

    public async Task DisconnectClient(string clientIdentifier)
    {
        await unitOfWork.Start(cs => cs.ClientIdentifier == clientIdentifier).ConfigureAwait(false);
        unitOfWork.AggregateRoot.Booted = false;
        await unitOfWork.Save();
    }
}