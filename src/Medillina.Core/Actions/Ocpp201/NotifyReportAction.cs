using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Core.Enums;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Actions.Ocpp201;

public sealed class NotifyReportAction(ILogger<NotifyReportAction> logger, IChargerConfigService service) : IOcppAction
{
    public string ActionName => OcppActionNames.NotifyReport;

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var request = call.As<NotifyReportRequest>();
        var reportCount = request.ReportData?.Count ?? 0;

        logger.LogInformation(
            "{ci}: Received NotifyReport requestId={rid} seqNo={seq} tbc={tbc} generatedAt={ga} reportDataCount={count}",
            clientIdentifier, request.RequestId, request.SeqNo, request.Tbc ?? false, request.GeneratedAt, reportCount);

        if (request.ReportData is not null)
        {
            var result = await service.IngestReport(clientIdentifier,
                request.RequestId, 
                request.SeqNo,
                request.Tbc ?? false,
                request.ReportData);

            switch (result)
            {
                case ReportNotificationIngestionResult.InvalidSeqNo:
                {
                    return new RpcResult()
                    {
                        Error = call.CreateErrorResult<NotifyReportResponse>(OcppCallError.ErrorCodes.OccurrenceConstraintViolation,
                            $"SeqNo {request.SeqNo} is invalid")
                    };
                }

                case ReportNotificationIngestionResult.InternalError:
                {
                    return new RpcResult()
                    {
                        Error = OcppCallError.InternalError(call.MessageId)
                    };
                }

                case ReportNotificationIngestionResult.MissingSeq:
                {
                    return new RpcResult()
                    {
                        Error = call.CreateErrorResult<NotifyReportResponse>(
                            OcppCallError.ErrorCodes.OccurrenceConstraintViolation,
                            $"Request {request.RequestId} marked as completed but we haven't received all the sequences")
                    };
                }
            }
        }

        return new RpcResult
        {
            Result = call.CreateResult(new NotifyReportResponse()),
            Error = null,
            ReturnToCS = true,
        };
    }
}
