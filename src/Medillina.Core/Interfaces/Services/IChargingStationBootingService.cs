using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Core.Enums;

namespace Medinilla.Core.Interfaces.Services;

public interface IChargingStationBootingService
{
    Task<BootupResult> ProcessBootup(string clientIdentifier, BootNotificationRequest request);

    Task DisconnectClient(string clientIdentifier);
}