using Medinilla.Core.Actions.Ocpp201;
using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Medinilla.Core.Tests;

public class TransactionEventActionShould
{
    private readonly Mock<ITransactionService> _service = new(MockBehavior.Strict);

    private TransactionEventAction CreateAction() =>
        new(NullLogger<TransactionEventAction>.Instance, _service.Object);

    private static OcppCallRequest CreateCall() => new("event-1", OcppActionNames.TransactionEvent, """
        {
            "eventType": "Updated",
            "timestamp": "2026-11-01T12:00:00Z",
            "triggerReason": "MeterValuePeriodic",
            "seqNo": 2,
            "transactionInfo": { "transactionId": "transaction-1" },
            "meterValue": [{
                "timestamp": "2026-11-01T11:59:59Z",
                "sampledValue": [{ "value": 1.5, "unitOfMeasure": { "unit": "kWh" } }]
            }]
        }
        """);

    [Fact]
    public void ExposeTransactionEventActionName()
    {
        Assert.Equal(OcppActionNames.TransactionEvent, CreateAction().ActionName);
    }

    [Fact]
    public async Task DelegateTheDeserializedRequestAndReturnTheServiceResponse()
    {
        TransactionEventRequest? capturedRequest = null;
        _service.Setup(s => s.IngestEvent("station-1", It.IsAny<TransactionEventRequest>()))
            .Callback<string, TransactionEventRequest>((_, request) => capturedRequest = request)
            .ReturnsAsync(new TransactionEventResponse
            {
                TotalCost = 12.50m,
                ChargingPriority = 2,
                IdTokenInfo = new IdTokenInfo { Status = AuthorizeStatus.Accepted },
            });
        var call = CreateCall();

        var result = await CreateAction().Execute(call, "station-1");

        Assert.NotNull(capturedRequest);
        Assert.Equal(TransactionEventEnum.Updated, capturedRequest.EventType);
        Assert.Equal(TriggerReasonEnum.MeterValuePeriodic, capturedRequest.TriggerReason);
        Assert.Equal("transaction-1", capturedRequest.TransactionInfo.TransactionId);
        Assert.Equal(2, capturedRequest.SeqNo);
        Assert.Equal(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc), capturedRequest.Timestamp);
        var meter = Assert.Single(capturedRequest.MeterValue!);
        Assert.Equal(new DateTime(2026, 11, 1, 11, 59, 59, DateTimeKind.Utc), meter.Timestamp);
        var sample = Assert.Single(meter.SampledValue);
        Assert.Equal(1.5m, sample.Value);
        Assert.Equal("kWh", sample.UnitOfMeasure.Unit);
        Assert.True(result.ReturnToCS);
        Assert.Null(result.Error);
        Assert.NotNull(result.Result);
        Assert.Equal(call.MessageId, result.Result.MessageId);
        var response = result.Result.As<TransactionEventResponse>();
        Assert.NotNull(response);
        Assert.Equal(12.50m, response.TotalCost);
        Assert.Equal(2, response.ChargingPriority);
        Assert.Equal(AuthorizeStatus.Accepted, response.IdTokenInfo?.Status);
        _service.Verify(s => s.IngestEvent("station-1", It.IsAny<TransactionEventRequest>()), Times.Once);
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReturnACallResultForAnEmptyServiceResponse()
    {
        _service.Setup(s => s.IngestEvent("station-1", It.IsAny<TransactionEventRequest>()))
            .ReturnsAsync(new TransactionEventResponse());

        var result = await CreateAction().Execute(CreateCall(), "station-1");

        Assert.True(result.ReturnToCS);
        Assert.Null(result.Error);
        Assert.NotNull(result.Result);
        var response = result.Result.As<TransactionEventResponse>();
        Assert.NotNull(response);
        Assert.Null(response.TotalCost);
        Assert.Null(response.IdTokenInfo);
    }

    [Fact]
    public async Task PropagateServiceExceptionsToTheRouter()
    {
        var expected = new InvalidOperationException("Ingestion failed.");
        _service.Setup(s => s.IngestEvent("station-1", It.IsAny<TransactionEventRequest>()))
            .ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateAction().Execute(CreateCall(), "station-1"));

        Assert.Same(expected, actual);
    }
}
