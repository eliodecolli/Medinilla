using Medinilla.WebApi.Interfaces;

namespace Medinilla.Core.WebApi.Services;

public class MessageQueueFactory : IMessageQueueFactory
{
    private readonly uint _ttl;

    public MessageQueueFactory(IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _ttl = ResolveTtl(config);
    }

    public IMessageQueue Create() => new MessageQueue(_ttl);

    private static uint ResolveTtl(IConfiguration config)
    {
        var value = config.GetSection("General")["MessageQueueTTL"];
        if (value is null) throw new InvalidOperationException("'MessageQueueTTL' configuration is not set.");
        if (!uint.TryParse(value, out var ttl)) throw new InvalidOperationException("'MessageQueueTTL' must be a non-negative integer.");
        return ttl;
    }
}
