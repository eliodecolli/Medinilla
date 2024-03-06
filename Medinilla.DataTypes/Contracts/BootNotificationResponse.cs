using Medinilla.DataTypes.Contracts.Common;
using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts;

public sealed class BootNotificationResponse
{
    [JsonConstructor]
    public BootNotificationResponse(int interval, RegistrationStatusEnum status, StatusInfo? statusInfo)
    {
        Interval = interval;
        Status = status;
        StatusInfo = statusInfo;
        CurrentTime = DateTime.Now;
    }

    public DateTime CurrentTime { get; private set; }
    
    // TODO: This is related to the heartbeat interval and some other stuff so you gotta revisit it.
    public int Interval { get; private set; }

    public RegistrationStatusEnum Status { get; private set; }

    public StatusInfo? StatusInfo { get; private set; }
}
