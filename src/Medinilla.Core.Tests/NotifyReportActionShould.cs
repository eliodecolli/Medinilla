using Medinilla.Core.Actions.Ocpp201;
using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Enums;
using Medinilla.Infrastructure;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Medinilla.Core.Tests;

public class NotifyReportActionShould
{
    private readonly Mock<IChargerConfigService> _service = new();

    [Fact]
    public async Task DelegateAnOmittedFinalReportToIngestionAsEmptyData()
    {
        _service.Setup(s => s.IngestReport(
                "station-1",
                7,
                0,
                false,
                It.Is<IEnumerable<ReportData>>(data => !data.Any())))
            .ReturnsAsync(ReportNotificationIngestionResult.Ok);

        var result = await CreateAction().Execute(CreateCall(), "station-1");

        Assert.True(result.ReturnToCS);
        Assert.Null(result.Error);
        Assert.NotNull(result.Result);
        Assert.NotNull(result.Result.As<NotifyReportResponse>());
        _service.Verify(s => s.IngestReport(
            "station-1",
            7,
            0,
            false,
            It.Is<IEnumerable<ReportData>>(data => !data.Any())), Times.Once);
    }

    private NotifyReportAction CreateAction() =>
        new(NullLogger<NotifyReportAction>.Instance, _service.Object);

    private static OcppCallRequest CreateCall()
    {
        var request = new NotifyReportRequest
        {
            RequestId = 7,
            SeqNo = 0,
            GeneratedAt = new DateTime(2026, 11, 14, 10, 30, 0, DateTimeKind.Utc),
            Tbc = null,
            ReportData = null,
        };

        return new OcppCallRequest(
            "message-1",
            OcppActionNames.NotifyReport,
            OcppPayloadSerializer.SerializePayload(request));
    }
}
