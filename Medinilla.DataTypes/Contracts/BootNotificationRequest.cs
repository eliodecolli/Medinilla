using Medinilla.DataTypes.Contracts.Common;
using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts;

public sealed class BootNotificationRequest
{
    [JsonConstructor]
    public BootNotificationRequest(BootReasonEnum reason, ChargingStation chargingStation)
    {
        Reason = reason;
        ChargingStation = chargingStation;
    }

    public BootReasonEnum Reason { get; private set; }

    public ChargingStation ChargingStation { get; private set; }
}
