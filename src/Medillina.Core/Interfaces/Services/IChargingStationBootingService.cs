using Medinilla.DataTypes.Contracts;

namespace Medinilla.Core.Interfaces.Services;

public interface IChargingStationBootingService
{
    Task ProcessBootup(string clientIdentifier, BootNotificationRequest request);

    Task DisconnectClient(string clientIdentifier);
}