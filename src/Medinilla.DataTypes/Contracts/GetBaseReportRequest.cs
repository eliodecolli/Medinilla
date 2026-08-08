using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

public class GetBaseReportRequest
{
    public int RequestId { get; set; }

    public ReportBaseEnum ReportBase { get; set; }
}
