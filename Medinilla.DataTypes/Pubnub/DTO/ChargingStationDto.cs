using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Pubnub.DTO;

public sealed class ChargingStationDto
{
    public string Id { get; set; }

    public string ClientIdentifier { get; set; }

    public string? Location { get; set; }

    public string? Alias { get; set; }

    public BootReasonEnum ChargingStatus { get; set; }
}
