namespace Medinilla.RealTime.Redis;

public static class RedisUtils
{
    public static string BuildChannelName(string prefix, string id)
    {
        return $"{prefix}-{id}";
    }

    public static string ProducerConnectionMultiplexer => "medinilla.producer";

    public static string ConsumerConnectionMultiplexer => "medinilla.consumer";

    /// <summary>
    /// Dedicated to the per-instance response queue drain. That loop is parked in a
    /// blocking BLPOP for the life of the process, so it must not share a connection
    /// with the general-purpose consumer.
    /// </summary>
    public static string SubscriptionConnectionMultiplexer => "medinilla.subscription";
}
