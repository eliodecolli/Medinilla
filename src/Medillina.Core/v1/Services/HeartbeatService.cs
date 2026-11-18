using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Contracts;

namespace Medinilla.Core.v1.Services;

public sealed class HeartbeatService : IHeartbeatService
{
    public Task<HeartbeatResponse> ProcessHeartbeat(string clientIdentifier) =>
        Task.FromResult(new HeartbeatResponse());
}
