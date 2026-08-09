using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.Core.Service.Communication.Mapping;

public partial class MedinillaMapping
{
    public static GetBaseReportRequest MapGetBaseReport(Medinilla.Core.gRPC.Service.GetBaseReportRequest request)
    {
        return new GetBaseReportRequest
        {
            RequestId = request.RequestId,
            ReportBase = Enum.TryParse<ReportBaseEnum>(request.ReportBase, out var rb)
                ? rb
                : ReportBaseEnum.FullInventory,
        };
    }
}
