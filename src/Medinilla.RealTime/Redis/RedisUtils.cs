namespace Medinilla.RealTime.Redis;

public static class RedisUtils
{
    public static string BuildChannelName(string prefix, string id)
    {
        return $"{prefix}-{id}";
    }
}
