using Medinilla.DataTypes.Contracts;

namespace Medinilla.Core.Interfaces.Services;

public interface IHeartbeatService
{
    Task<HeartbeatResponse> ProcessHeartbeat(string clientIdentifier);
}
