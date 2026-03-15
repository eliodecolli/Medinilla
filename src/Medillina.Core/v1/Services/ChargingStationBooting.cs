using System.Text.Json;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.Logic.Configuration;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts;

namespace Medinilla.Core.v1.Services;

public class ChargingStationBooting(ChargingStationUnitOfWork unitOfWork) : IChargingStationBootingService
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

    private async Task TryBootstrapChargingStatation(ChargingStation entity)
    {
        var medinillaSettings = CentralConfig.GetMedinillaConfiguration();

        if (entity.Tariffs is null || entity.Tariffs.Count == 0)
        {
            // get default unit price
            var defaultUnit = medinillaSettings.DefaultUnit;

            await unitOfWork.TariffsRepository.Create(new Tariff()
            {
                Id = Guid.NewGuid(),
                ChargingStationId = entity.Id,
                UnitName = defaultUnit.Name,
                UnitPrice = (decimal)defaultUnit.Price,
            });
        }

        if (entity.AuthorizationDetails is null)
        {
            await unitOfWork.AuthDetailsRepository.Create(new AuthorizationDetails()
            {
                AuthBlob = JsonDocument.Parse(medinillaSettings.DefaultAuthDetails ?? "{}"),
                ChargingStationId = entity.Id,
            });
        }

        if ((entity.IdTokens is null || entity.IdTokens.Count == 0) && medinillaSettings.UseDefaultUser)
        {
            var defaultUser = medinillaSettings.DefaultUser;
            var entityUser = await unitOfWork.AuthorizationUserRepository.Create(new AuthorizationUser()
            {
                ChargingStationId = entity.Id,
                ActiveCredit = (decimal)defaultUser.ActiveCredit,
                DisplayName = defaultUser.DisplayName,
                IsActive = true
            });

            await unitOfWork.IdTokenRepository.Create(new IdToken()
            {
                ChargingStationId = entity.Id,
                AuthorizationUserId = entityUser.Id,
                Token = defaultUser.Token,
                CreatedDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(100000),
                IdType = "ISO14443"
            });
        }

        await unitOfWork.ChargingStationRepository.Update(entity);
        await unitOfWork.Save();
    }
    
    public async Task ProcessBootup(string clientIdentifier, BootNotificationRequest request)
    {
        var result = await unitOfWork.ChargingStationRepository.Filter(c => c.ClientIdentifier == clientIdentifier);
        var entity = result.FirstOrDefault();
        
        if (entity == null)
        {
            entity = GetChargingStation(clientIdentifier, request);
            var accountQuery = await unitOfWork.AccountRepository.Filter(c => c.Name == "Main Test Account").ConfigureAwait(false);
            var account = accountQuery.First();
            entity.AccountId = account.Id;

            entity.CreatedAt = DateTime.UtcNow;
            entity = await unitOfWork.ChargingStationRepository.Create(entity);
        }
        else
        {
            entity.LatestBootNotificationReason = GetBootupReason(request);
            entity.ModifiedAt = DateTime.UtcNow;
            entity.Booted = true;
        }

        await TryBootstrapChargingStatation(entity);
    }

    public async Task DisconnectClient(string clientIdentifier)
    {
        var chargingStation =
            (await unitOfWork.ChargingStationRepository.Filter(cs => cs.ClientIdentifier == clientIdentifier)
                .ConfigureAwait(false)).FirstOrDefault();
        
        if (chargingStation is not null)
        {
            chargingStation.Booted = false;
            await unitOfWork.ChargingStationRepository.Update(chargingStation);
            await unitOfWork.Save();
        }
    }
}