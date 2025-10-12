using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Medinilla.Core.Logic;
using Medinilla.Core.Logic.Configuration;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Pubnub;
using Medinilla.DataTypes.Pubnub.DTO;
using Medinilla.Infrastructure.WAMP;
using Medinilla.RealTime;
using Medinilla.Services.Actions;
using Microsoft.Extensions.Logging;
using CS = Medinilla.DataAccess.Relational.Models.ChargingStation;
using IdTokenDb = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;

namespace Medinilla.Core.Actions.Ocpp201;

public sealed class BootNotificationAction(ChargingStationUnitOfWork unitOfWork,
    ILogger<BootNotificationAction> _logger,
    ICommunicationProvider commsProvider) : IOcppAction
{
    public string ActionName { get => "BootNotification"; }

    private CS GetChargingStation(string clientIdentifier, BootNotificationRequest request)
    {
        return new CS()
        {
            ClientIdentifier = clientIdentifier,
            Model = request.ChargingStation.Model,
            Vendor = request.ChargingStation.VendorName,
            LatestBootNotificationReason = Enum.GetName(request.Reason)!,
        };
    }

    #region PubNub
    private async Task PublishChargingStationToPubnub(CS chargingStation)
    {
        var lastBootReason = BootReasonEnum.Unknown;
        if (Enum.TryParse<BootReasonEnum>(chargingStation.LatestBootNotificationReason, out var reason))
        {
            lastBootReason = reason;
        }
        else
        {
            _logger.LogWarning($"Could not read boot reason for client identifier {chargingStation.ClientIdentifier}: Value {chargingStation.LatestBootNotificationReason}");
        }

        var pubnubMessage = new PubnubMessage<ChargingStationDto>()
        {
            Header = PubnubMessageHeader.Set,
            Payload = new ChargingStationDto()
            {
                Id = chargingStation.Id.ToString(),
                ClientIdentifier = chargingStation.ClientIdentifier,
                Location = chargingStation.Location,
                Alias = chargingStation.Alias,
                ChargingStatus = lastBootReason,
            }
        };

        var pubnub = commsProvider.GetMessenger("PubNub");
        if (pubnub is null)
        {
            _logger.LogError("PubNub messenger not found");
            return;
        }

        var payload = JsonSerializer.Serialize(pubnubMessage, new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter<BootReasonEnum>() }
        });
        var buffer = Encoding.UTF8.GetBytes(payload);

        var channel = RealTimeChannels.GetChargingStationsChannel(chargingStation.AccountId.ToString());

        _logger.LogInformation($"Publishing charging station event to PubNub channel {channel}");
        await pubnub.SendMessage(channel, buffer);
    }
    #endregion


    #region BusinessLogic

    // TODO: This should ONLY cover the regular boot notification processing, not creating default values
    private async Task<CS> ProcessBootNotification(CS chargingStation)
    {
        var result = await unitOfWork.ChargingStationRepository.Filter(c => c.ClientIdentifier == chargingStation.ClientIdentifier);
        var entity = result.FirstOrDefault();
        if (entity == null)
        {
            var accountQuery = await unitOfWork.AccountRepository.Filter(c => c.Name == "Main Test Account").ConfigureAwait(false);
            var account = accountQuery.First();
            chargingStation.AccountId = account.Id;

            chargingStation.CreatedAt = DateTime.UtcNow;
            entity = await unitOfWork.ChargingStationRepository.Create(chargingStation);
        }
        else
        {
            entity.LatestBootNotificationReason = chargingStation.LatestBootNotificationReason;
            entity.ModifiedAt = DateTime.UtcNow;
        }

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

            await unitOfWork.IdTokenRepository.Create(new IdTokenDb()
            {
                ChargingStationId = entity.Id,
                AuthorizationUserId = entityUser.Id,
                Token = defaultUser.Token,
                CreatedDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(100000),
                IdType = "ISO14443"
            });
        }

        return entity;
    }

    #endregion

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var notification = call.As<BootNotificationRequest>();
        _logger.LogInformation("Received boot notification {0} from {3}. Vendor: {1} Model: {2}",
            notification.Reason,
            notification.ChargingStation is not null ? notification.ChargingStation.VendorName : "UNDEFINED",
            notification.ChargingStation is not null ? notification.ChargingStation.Model : "UNDEFINED",
            clientIdentifier);

        try
        {
            var chargingStation = await ProcessBootNotification(GetChargingStation(clientIdentifier, notification));
            await unitOfWork.Save();
        }
        catch (Exception ex)
        {
            return new RpcResult()
            {
                Result = call.CreateResult(new BootNotificationResponse(1440, RegistrationStatusEnum.Rejected, new StatusInfo()
                {
                    ReasonCode = "500",
                    AdditionalInfo = "Internal System Error",
                })),
                Error = null,
                ReturnToCS = true,
            };
        }

        // idk do something here..?
        return new RpcResult()
        {
            Result = call.CreateResult(new BootNotificationResponse(1440, RegistrationStatusEnum.Accepted, new StatusInfo()
            {
                ReasonCode = "200",
                AdditionalInfo = ""
            })),
            Error = null,
            ReturnToCS = true
        };
    }
}
