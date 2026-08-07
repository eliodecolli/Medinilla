namespace Medinilla.RealTime;

/// <summary>
/// Maps a charger to the response queue of the WebApi instance currently hosting
/// its WebSocket. Entries are TTL'd; the owning instance keeps them alive.
/// </summary>
public interface IWebSocketRoutingTable
{
    Task RegisterAsync(string clientId, string queue, CancellationToken hostCt = default);

    Task UnregisterAsync(string clientId, CancellationToken ct = default);

    /// <summary>Returns null when no instance currently hosts the charger.</summary>
    Task<string?> GetResponseQueueAsync(string clientId, CancellationToken ct = default);

    /// <summary>SETEX primitive — the caller drives the cadence.</summary>
    Task RefreshEntryAsync(string clientId, CancellationToken ct = default);
}
