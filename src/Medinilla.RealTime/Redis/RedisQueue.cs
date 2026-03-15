using StackExchange.Redis;

namespace Medinilla.RealTime.Redis;

public sealed class RedisQueue : IRedisQueue
{
    private readonly IDatabase _db;
    private readonly ConnectionMultiplexer? _ownedMux;

    private const string RPUSH = "RPUSH";
    private const string BRPOP = "BRPOP";
    private const int BrpopTimeoutSeconds = 5;

    /// <summary>
    /// Creates a queue backed by a shared (externally owned) multiplexer.
    /// Used for outbound/RPUSH where sharing the connection is safe.
    /// </summary>
    public RedisQueue(ConnectionMultiplexer mux)
    {
        _db = mux.GetDatabase();
        _ownedMux = null;
    }

    /// <summary>
    /// Creates a queue with its own dedicated connection.
    /// Used for inbound/BRPOP to avoid blocking the shared connection.
    /// </summary>
    public RedisQueue(string connectionString)
    {
        _ownedMux = ConnectionMultiplexer.Connect(connectionString);
        _db = _ownedMux.GetDatabase();
    }

    /// <summary>
    /// Blocks until a message is available, using a finite server-side timeout so
    /// the connection is never held open indefinitely and cancellation is responsive.
    /// </summary>
    public async Task<byte[]?> WaitForMessage(string channel, CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            var result = await _db.ExecuteAsync(BRPOP, channel, BrpopTimeoutSeconds.ToString())
                                  .WaitAsync(ct);

            if (result is not null && !result.IsNull)
            {
                return (byte[])result[1]!;
            }
            // null = server-side timeout with no data; loop and retry
        }

        return null;
    }

    public async Task SendMessage(byte[] message, string channel)
    {
        await _db.ExecuteAsync(RPUSH, channel, message);
    }

    public void Dispose() => _ownedMux?.Dispose();
}
