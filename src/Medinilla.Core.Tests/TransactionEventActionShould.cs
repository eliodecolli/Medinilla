using Medinilla.Core.Actions.Ocpp201;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging.Abstractions;

namespace Medinilla.Core.Tests;

public class TransactionEventActionShould
{
    [Fact]
    public void ExposeTransactionEventActionName()
    {
        var action = new TransactionEventAction(NullLogger<TransactionEventAction>.Instance);

        Assert.Equal(OcppActionNames.TransactionEvent, action.ActionName);
    }

    [Fact]
    public async Task ReturnNotSupportedUntilProcessingIsImplemented()
    {
        var action = new TransactionEventAction(NullLogger<TransactionEventAction>.Instance);
        var call = new OcppCallRequest("event-1", OcppActionNames.TransactionEvent, """
            {
                "eventType": "Started",
                "timestamp": "2026-11-01T12:00:00Z",
                "triggerReason": "Authorized",
                "seqNo": 0,
                "transactionInfo": { "transactionId": "transaction-1" }
            }
            """);

        var result = await action.Execute(call, "station-1");

        Assert.True(result.ReturnToCS);
        Assert.Null(result.Result);
        Assert.NotNull(result.Error);
        Assert.Equal(OcppCallError.ErrorCodes.NotSupported, result.Error.ErrorCode);
        Assert.Equal(call.MessageId, result.Error.MessageId);
    }
}
