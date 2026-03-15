namespace Medinilla.Core.Interfaces.Services;

public interface IRouterServices
{
    Task<bool> ValidateChargingStationAvailability(string clientIdentifier);
    
    Task DisconnectClient(string clientIdentifier);
}