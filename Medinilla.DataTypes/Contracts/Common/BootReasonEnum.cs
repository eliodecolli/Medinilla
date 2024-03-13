using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BootReasonEnum
{
    ApplicationReset,
    FirmwareUpdate,
    LocalReset,
    PowerUp,
    RemoteReset,
    ScheduledReset,
    Triggered,
    Unknown,
    Watchdog
}
