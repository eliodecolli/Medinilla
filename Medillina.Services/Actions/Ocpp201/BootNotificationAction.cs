using Medinilla.Core.Logic;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Pubnub;
using Medinilla.DataTypes.Pubnub.DTO;
using Medinilla.Infrastructure.WAMP;
using Medinilla.RealTime;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CS = Medinilla.DataAccess.Relational.Models.ChargingStation;

namespace Medinilla.Services.Actions.Ocpp201;

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

    private async Task PublishChargingStationToPubnub(CS chargingStation)
    {
        var pubnubMessage = new PubnubMessage<ChargingStationDto>()
        {
            Header = PubnubMessageHeader.Set,
            Payload = new ChargingStationDto()
            {
                Id = chargingStation.Id.ToString(),
                ClientIdentifier = chargingStation.ClientIdentifier,
                Location = chargingStation.Location,
                Alias = chargingStation.Alias,
                ChargingStatus = ChargingStatusEnum.Inactive,
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
            Converters = { new JsonStringEnumConverter<ChargingStatusEnum>() }
        });
        var buffer = Encoding.UTF8.GetBytes(payload);

        var channel = RealTimeChannels.GetChargingStationsChannel(chargingStation.AccountId.ToString());

        _logger.LogInformation($"Publishing charging station event to PubNub channel {channel}");
        await pubnub.SendMessage(channel, buffer);
    }

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var notification = call.As<BootNotificationRequest>();
        _logger.LogInformation("Received boot notification {0} from {3}. Vendor: {1} Model: {2}",
            notification.Reason,
            notification.ChargingStation is not null ? notification.ChargingStation.VendorName : "UNDEFINED",
            notification.ChargingStation is not null ? notification.ChargingStation.Model : "UNDEFINED",
            clientIdentifier);

        var chargingStation = await unitOfWork.ProcessBootNotification(GetChargingStation(clientIdentifier, notification));
        await unitOfWork.Save();

        await PublishChargingStationToPubnub(chargingStation);

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
