namespace Medinilla.DataTypes.Contracts;

public sealed class HeartbeatResponse
{
    public HeartbeatResponse()
    {
        var dtPattern = "yyyy-MM-ddThh:mm:ss";
        var currentDateTime = DateTime.Now.ToString(dtPattern);

        CurrentTime = DateTime.ParseExact(currentDateTime, dtPattern, System.Globalization.CultureInfo.InvariantCulture);
    }

    public DateTime CurrentTime { get; private set; }
}
