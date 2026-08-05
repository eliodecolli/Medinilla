using Medinilla.Core.v1;
using Medinilla.RealTime.Redis;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Medinilla.Core.Service.Communication;

internal class RedisRoutingTable(IServiceProvider sp) : BaseOcppRoutingTable
{
    private readonly ConnectionMultiplexer? mux = sp.GetKeyedService<ConnectionMultiplexer>(RedisUtils.ProducerConnectionMultiplexer);

    private void AssertMux()
    {
        if (mux is null) throw new InvalidOperationException("Connection Mux is not initialized.");
    }

    private string GetKey(string messageId) => $"routing:out:{messageId}";

    public override async Task Add(string messageId, string value)
    {
        AssertMux();
        var connection = mux!.GetDatabase();
        await connection.StringSetAsync(GetKey(messageId), value);
    }

    public override async Task Remove(string messageId)
    {
        AssertMux();
        var connection = mux!.GetDatabase();
        await connection.KeyDeleteAsync(GetKey(messageId));
    }

    public override async Task<string?> TryGetValue(string messageId)
    {
        AssertMux();
        var connection = mux!.GetDatabase();
        var val = await connection.StringGetAsync(GetKey(messageId));

        return val.HasValue ? val.ToString() : null;
    }
}
