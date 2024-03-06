namespace Medinilla.DataTypes.Contracts.Common;

public sealed class ChargingStation
{
    public string SerialNumber { get; set; }

    public string Model { get; set; }

    public string VendorName { get; set; }

    public string FirmwareVersion { get; set; }

    public Modem Modem { get; set; }
}
