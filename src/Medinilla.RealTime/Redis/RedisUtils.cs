namespace Medinilla.RealTime.Redis;

public static class RedisUtils
{
    public static string BuildChannelName(string prefix, string id)
    {
        return $"{prefix}-{id}";
    }

    public static string ProducerConnectionMultiplexer => "medinilla.producer";

    public static string ConsumerConnectionMultiplexer => "medinilla.consumer";
}
