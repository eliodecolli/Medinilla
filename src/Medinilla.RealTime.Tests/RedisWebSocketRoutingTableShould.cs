using Medinilla.RealTime.Redis;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Medinilla.RealTime.Tests;

public class RedisWebSocketRoutingTableShould
{
    private const string CLIENT_ID = "CHARGER-A";
    private const string QUEUE = "medinilla.ws.deadbeef.response";
    private const string KEY = "medinilla.ws.routing.CHARGER-A";

    private static readonly TimeSpan ExpectedTtl = TimeSpan.FromSeconds(60);

    private readonly Mock<IDatabase> _dbMock = new();
    private readonly Mock<ILogger<RedisWebSocketRoutingTable>> _loggerMock = new();

    public RedisWebSocketRoutingTableShould()
    {
        _dbMock
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _dbMock
            .Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _dbMock
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
    }

    private RedisWebSocketRoutingTable CreateTable()
        => new(_dbMock.Object, _loggerMock.Object);

    [Fact]
    public async Task SetTheKeyWithTtlOnRegister()
    {
        var table = CreateTable();

        await table.RegisterAsync(CLIENT_ID, QUEUE);

        _dbMock.Verify(db => db.StringSetAsync(
            KEY, QUEUE, ExpectedTtl,
            It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);

        await table.UnregisterAsync(CLIENT_ID);
    }

    [Fact]
    public async Task ExtendTtlOnRefresh()
    {
        var table = CreateTable();
        await table.RegisterAsync(CLIENT_ID, QUEUE);

        await table.RefreshEntryAsync(CLIENT_ID);

        // Once for the register, once for the refresh — both with the full TTL.
        _dbMock.Verify(db => db.StringSetAsync(
            KEY, QUEUE, ExpectedTtl,
            It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Exactly(2));

        await table.UnregisterAsync(CLIENT_ID);
    }

    [Fact]
    public async Task IgnoreRefreshForUnknownClient()
    {
        var table = CreateTable();

        await table.RefreshEntryAsync("NEVER-REGISTERED");

        _dbMock.Verify(db => db.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
            It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task DeleteTheKeyOnUnregister()
    {
        var table = CreateTable();
        await table.RegisterAsync(CLIENT_ID, QUEUE);

        await table.UnregisterAsync(CLIENT_ID);

        _dbMock.Verify(db => db.KeyDeleteAsync(KEY, It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task StopRefreshingAfterUnregister()
    {
        var table = CreateTable();
        await table.RegisterAsync(CLIENT_ID, QUEUE);
        await table.UnregisterAsync(CLIENT_ID);

        await table.RefreshEntryAsync(CLIENT_ID);

        // Only the original register wrote the key; the entry is gone.
        _dbMock.Verify(db => db.StringSetAsync(
            KEY, QUEUE, ExpectedTtl,
            It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task DeleteTheKeyEvenWhenClientWasNeverRegistered()
    {
        var table = CreateTable();

        await table.UnregisterAsync(CLIENT_ID);

        _dbMock.Verify(db => db.KeyDeleteAsync(KEY, It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ReturnTheQueueForAKnownClient()
    {
        _dbMock
            .Setup(db => db.StringGetAsync(KEY, It.IsAny<CommandFlags>()))
            .ReturnsAsync(QUEUE);

        var table = CreateTable();

        Assert.Equal(QUEUE, await table.GetResponseQueueAsync(CLIENT_ID));
    }

    [Fact]
    public async Task ReturnNullForAnUnknownClient()
    {
        var table = CreateTable();

        Assert.Null(await table.GetResponseQueueAsync("NOBODY"));
    }

    // An expired key is indistinguishable from an absent one at this layer: Redis
    // drops it and StringGetAsync comes back null.
    [Fact]
    public async Task ReturnNullOnceTheEntryHasExpired()
    {
        var expired = false;
        _dbMock
            .Setup(db => db.StringGetAsync(KEY, It.IsAny<CommandFlags>()))
            .ReturnsAsync(() => expired ? RedisValue.Null : QUEUE);

        var table = CreateTable();
        await table.RegisterAsync(CLIENT_ID, QUEUE);

        Assert.Equal(QUEUE, await table.GetResponseQueueAsync(CLIENT_ID));

        expired = true;
        Assert.Null(await table.GetResponseQueueAsync(CLIENT_ID));

        await table.UnregisterAsync(CLIENT_ID);
    }

    // Re-registering the same charger (reconnect) must not leave two refresh loops behind.
    [Fact]
    public async Task ReplaceTheEntryWhenTheSameClientRegistersTwice()
    {
        const string newQueue = "medinilla.ws.cafebabe.response";

        var table = CreateTable();
        await table.RegisterAsync(CLIENT_ID, QUEUE);
        await table.RegisterAsync(CLIENT_ID, newQueue);

        await table.RefreshEntryAsync(CLIENT_ID);

        _dbMock.Verify(db => db.StringSetAsync(
            KEY, newQueue, ExpectedTtl,
            It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Exactly(2));

        await table.UnregisterAsync(CLIENT_ID);
    }
}
