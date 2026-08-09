using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Enums;

namespace Medinilla.Core.Interfaces.Services;

public interface IChargerConfigService
{
    Task<ReportNotificationIngestionResult> IngestReport(string clientIdentifier, int requestId, int seqNumber, bool tbc, IEnumerable<ReportData> data);
}