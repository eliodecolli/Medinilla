using StackExchange.Redis;

namespace Medinilla.RealTime.Redis;

public sealed class RedisQueue : IMessageQueue
{
    private readonly IDatabase _db;
    private readonly ConnectionMultiplexer? _ownedMux;

    private const string RPUSH = "RPUSH";
    private const string BRPOP = "BRPOP";
    private const int BrpopTimeoutSeconds = 5;

    public RedisQueue(ConnectionMultiplexer mux)
    {
        _db = mux.GetDatabase();
        _ownedMux = null;
    }

    public RedisQueue(string connectionString)
    {
        _ownedMux = ConnectionMultiplexer.Connect(connectionString);
        _db = _ownedMux.GetDatabase();
    }

    public async Task<byte[]?> ReceiveAsync(string queue, CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            var result = await _db.ExecuteAsync(BRPOP, queue, BrpopTimeoutSeconds.ToString())
                                  .WaitAsync(ct);

            if (result is not null && !result.IsNull)
            {
                return (byte[])result[1]!;
            }
        }

        return null;
    }

    public async Task SendAsync(byte[] message, string queue, CancellationToken ct = default)
    {
        await _db.ExecuteAsync(RPUSH, queue, message).WaitAsync(ct);
    }

    public void Dispose() => _ownedMux?.Dispose();
}
