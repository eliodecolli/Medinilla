using Medinilla.DataTypes.Contracts.Common;
using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts;

[method: JsonConstructor]
public sealed class BootNotificationRequest(BootReasonEnum reason, ChargingStation chargingStation)
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BootReasonEnum Reason { get; private set; } = reason;

    public ChargingStation ChargingStation { get; private set; } = chargingStation;
}
