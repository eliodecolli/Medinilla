using StackExchange.Redis;

namespace Medinilla.RealTime.Redis;

public sealed class RedisSender : ISender
{
    private readonly IDatabase _db;

    private const string RPUSH = "RPUSH";

    public RedisSender(ConnectionMultiplexer producerMux)
    {
        _db = producerMux.GetDatabase();
    }

    public async Task SendAsync(string queue, byte[] message, CancellationToken ct = default)
    {
        await _db.ExecuteAsync(RPUSH, queue, message).WaitAsync(ct);
    }

    public void Dispose() { }
}

public sealed class RedisReceiver : IReceiver
{
    private readonly IDatabase _db;

    private const string BRPOP = "BRPOP";
    private const int BrpopTimeoutSeconds = 3;

    public RedisReceiver(ConnectionMultiplexer consumerMux)
    {
        _db = consumerMux.GetDatabase();
    }

    public async Task<byte[]?> ReceiveAsync(string queue, CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _db.ExecuteAsync(BRPOP, queue, BrpopTimeoutSeconds.ToString())
                    .WaitAsync(ct);
                if (result is not null && !result.IsNull)
                {
                    return (byte[])result[1]!;
                }
            }
            catch (RedisTimeoutException)
            {
            }
        }

        return null;
    }

    public void Dispose() { }
}