using System.Security.Cryptography;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.Core.Service.Communication.Mapping;

public partial class MedinillaMapping
{
    public static GetBaseReportRequest MapGetBaseReport(Medinilla.Core.gRPC.Service.GetBaseReportRequest request)
    {
        return new GetBaseReportRequest
        {
            // the charger echoes this back on every NotifyReport, and we key the in-flight
            // report state on it - so it has to not collide with a report still being received.
            RequestId = RandomNumberGenerator.GetInt32(1, int.MaxValue),
            ReportBase = Enum.TryParse<ReportBaseEnum>(request.ReportBase, out var rb)
                ? rb
                : ReportBaseEnum.FullInventory,
        };
    }
}
