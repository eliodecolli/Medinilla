using Medinilla.WebApi.Interfaces;

namespace Medinilla.Core.WebApi.Services;

/// <summary>
/// Generated once per host start. Deliberately not configurable (config would
/// collide across replicas) and not persisted (a new instance is a new queue;
/// orphaned routing entries TTL out on their own).
/// </summary>
internal sealed class InstanceIdentifier : IInstanceIdentifier
{
    public string InstanceId { get; } = Guid.NewGuid().ToString("N")[..8];

    public string ResponseQueue => $"medinilla.ws.{InstanceId}.response";
}
