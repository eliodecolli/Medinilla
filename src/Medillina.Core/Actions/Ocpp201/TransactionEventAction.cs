using Medinilla.Core.Events;
using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Contracts;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Actions.Ocpp201;

public sealed class TransactionEventAction(
    ILogger<TransactionEventAction> logger,
    ITransactionService service) : IOcppAction
{
    public string ActionName => OcppActionNames.TransactionEvent;

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var request = call.As<TransactionEventRequest>();
        logger.LogInformation(
            "{ClientIdentifier}: Received transaction event {EventType} for transaction {TransactionId}, sequence {SeqNo}.",
            clientIdentifier, request.EventType, request.TransactionInfo.TransactionId, request.SeqNo);
        
        var ingestion = await service.IngestEvent(clientIdentifier, request);
        
        return new RpcResult()
        {
            Result = call.CreateResult(ingestion.Response),
            ReturnToCS = true,
            ProcessingContext = new TransactionEventProcessingContext(ingestion.WasNewlyIngested),
        };
    }
}
