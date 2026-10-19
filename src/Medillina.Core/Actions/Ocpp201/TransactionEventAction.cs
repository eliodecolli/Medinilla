using Medinilla.DataTypes.Contracts;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Actions.Ocpp201;

public sealed class TransactionEventAction(ILogger<TransactionEventAction> logger) : IOcppAction
{
    public string ActionName => OcppActionNames.TransactionEvent;

    public Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var request = call.As<TransactionEventRequest>();
        logger.LogInformation(
            "{ClientIdentifier}: Received transaction event {EventType} for transaction {TransactionId}, sequence {SeqNo}.",
            clientIdentifier, request.EventType, request.TransactionInfo.TransactionId, request.SeqNo);

        return Task.FromResult(new RpcResult
        {
            Error = call.CreateErrorResult<TransactionEventResponse>(
                OcppCallError.ErrorCodes.NotSupported,
                "Transaction event processing is not implemented yet."),
            ReturnToCS = true,
        });
    }
}
