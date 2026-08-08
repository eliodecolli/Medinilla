using Medinilla.DataTypes.Contracts;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Actions.Ocpp201;

public sealed class NotifyReportAction(ILogger<NotifyReportAction> logger) : IOcppAction
{
    public string ActionName => OcppActionNames.NotifyReport;

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var request = call.As<NotifyReportRequest>();
        var reportCount = request.ReportData?.Count ?? 0;

        logger.LogInformation(
            "{ci}: Received NotifyReport requestId={rid} seqNo={seq} tbc={tbc} generatedAt={ga} reportDataCount={count}",
            clientIdentifier, request.RequestId, request.SeqNo, request.Tbc ?? false, request.GeneratedAt, reportCount);

        // TODO: implement business logic (correlate by requestId, persist reportData, handle tbc/seqNo flow, etc.)

        return new RpcResult
        {
            Result = call.CreateResult(new NotifyReportResponse()),
            Error = null,
            ReturnToCS = true,
        };
    }
}
