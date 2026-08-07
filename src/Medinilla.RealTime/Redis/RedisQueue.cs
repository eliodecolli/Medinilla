using Microsoft.Extensions.Logging;
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
    private readonly ILogger<IReceiver> _log;

    private const string BLPOP = "BLPOP";
    private const int BlpopTimeoutSeconds = 3;

    public RedisReceiver(ConnectionMultiplexer consumerMux, ILogger<IReceiver> log)
    {
        _db = consumerMux.GetDatabase();
        _log = log;
    }

    public async Task<byte[]?> ReceiveAsync(string queue, CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _db.ExecuteAsync(BLPOP, queue, BlpopTimeoutSeconds.ToString())
                    .WaitAsync(ct);
                if (result is not null && !result.IsNull)
                {
                    return (byte[])result[1]!;
                }
            }
            catch (RedisTimeoutException)
            {
            }
            catch (Exception ex)
            {
                _log.LogError("RedisReceiver: {queue}: {error}", queue, ex.Message);
            }
        }

        return null;
    }

    public void Dispose() { }
}