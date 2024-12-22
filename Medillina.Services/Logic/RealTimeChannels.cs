namespace Medinilla.Core.Logic;

public static class RealTimeChannels
{
    public static string GetChargingStationsChannel(string accountId)
    {
        return $"medinilla.core.{accountId}-charging-stations";
    }
}
