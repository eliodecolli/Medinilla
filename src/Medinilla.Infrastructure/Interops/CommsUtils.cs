namespace Medinilla.Infrastructure.Interops;

public static class CommsUtils
{
    public static string GetChannelName(string clientIdentification) => $"ws.{clientIdentification}";

    public static string GetAppChannelName() => "medinilla.app";

    public static string GetResponseChannelName(string channelName) => $"{channelName}.response";
}
