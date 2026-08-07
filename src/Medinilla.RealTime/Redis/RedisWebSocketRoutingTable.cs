using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace Medinilla.RealTime.Redis;

internal sealed class RedisWebSocketRoutingTable(
    IDatabase db,
    ILogger<RedisWebSocketRoutingTable> logger) : IWebSocketRoutingTable
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RefreshDue = TimeSpan.FromSeconds(30);

    private const string KeyPrefix = "medinilla.ws.routing";

    private sealed record Entry(string Queue, CancellationTokenSource Cts);

    private static string Key(string clientId) => $"{KeyPrefix}.{clientId}";

    public async Task RegisterAsync(string clientId, string queue, CancellationToken hostCt = default)
    {
        await db.StringSetAsync(Key(clientId), queue, Ttl);

        // A re-register for the same charger supersedes the previous refresh loop.
        var cts = CancellationTokenSource.CreateLinkedTokenSource(hostCt);
        var entry = new Entry(queue, cts);

        if (_entries.TryRemove(clientId, out var previous))
        {
            previous.Cts.Cancel();
        }

        _entries[clientId] = entry;

        _ = Task.Run(() => RefreshLoop(clientId, entry), CancellationToken.None);
    }

    public async Task UnregisterAsync(string clientId, CancellationToken ct = default)
    {
        if (_entries.TryRemove(clientId, out var entry))
        {
            // The loop owns disposal, so cancelling here cannot race it.
            entry.Cts.Cancel();
        }

        await db.KeyDeleteAsync(Key(clientId));
    }

    public async Task<string?> GetResponseQueueAsync(string clientId, CancellationToken ct = default)
        => await db.StringGetAsync(Key(clientId));

    public Task RefreshEntryAsync(string clientId, CancellationToken ct = default)
    {
        if (_entries.TryGetValue(clientId, out var entry))
        {
            return db.StringSetAsync(Key(clientId), entry.Queue, Ttl);
        }

        return Task.CompletedTask;
    }

    private async Task RefreshLoop(string clientId, Entry entry)
    {
        var ct = entry.Cts.Token;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(RefreshDue, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                try
                {
                    await RefreshEntryAsync(clientId, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Refresh failed for {ClientId}", clientId);
                }
            }
        }
        finally
        {
            entry.Cts.Dispose();
        }
    }
}
