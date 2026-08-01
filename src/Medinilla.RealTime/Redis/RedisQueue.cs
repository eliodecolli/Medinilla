using StackExchange.Redis;

namespace Medinilla.RealTime.Redis;

public sealed class RedisQueue : ISender, IReceiver
{
    private readonly IDatabase _db;

    private const string RPUSH = "RPUSH";
    private const string BRPOP = "BRPOP";
    private const int BrpopTimeoutSeconds = 5;

    public RedisQueue(ConnectionMultiplexer mux)
    {
        _db = mux.GetDatabase();
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

    public async Task SendAsync(string queue, byte[] message, CancellationToken ct = default)
    {
        await _db.ExecuteAsync(RPUSH, queue, message).WaitAsync(ct);
    }

    public void Dispose() { }
}
