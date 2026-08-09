using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

public class GetBaseReportResponse
{
    public GenericDeviceModelStatusEnum Status { get; set; }

    public StatusInfo StatusInfo { get; set; }
}
