using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

public sealed class NotifyReportRequest
{
    public int RequestId { get; set; }

    public DateTime GeneratedAt { get; set; }

    public bool? Tbc { get; set; }

    public int SeqNo { get; set; }

    public List<ReportData>? ReportData { get; set; }
}
