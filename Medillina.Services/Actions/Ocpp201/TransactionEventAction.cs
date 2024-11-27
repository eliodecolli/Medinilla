using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.WAMP;
using Microsoft.Extensions.Logging;

using DbTransaction = Medinilla.DataAccess.Relational.Models.Transaction;

namespace Medinilla.Services.Actions.Ocpp201;

public sealed class TransactionEventAction(ILogger<TransactionEventAction> _logger, TransactionsUnitOfWork unitOfWork)
    : IOcppAction
{

    public string ActionName => "TransactionEvent";

    private DbTransaction MapOcppTransaction(TransactionEventRequest request)
    {
        return new DbTransaction()
        {
            TransactionId = request.TransactionInfo.TransactionId,
            SeqNo = request.SeqNo,
            Timestamp = request.Timestamp,
            IdToken = request.IdToken?.Token,
            EVSEId = request.Evse?.Id,
            Offline = request.Offline,
        };
    }

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var request = call.As<TransactionEventRequest>();
        switch(request.EventType)
        {
            case TransactionEventEnum.Updated:
                _logger.LogInformation($"{clientIdentifier}: Received transaction update status.");
                break;
            case TransactionEventEnum.Started:
                _logger.LogInformation($"{clientIdentifier} started a new transaction. Reason: {request.TriggerReason}");
                break;
            case TransactionEventEnum.Ended:
                _logger.LogInformation($"{clientIdentifier} ended transaction with id {request.TransactionInfo.TransactionId}");
                break;
        }

        // TODO: Implement checks here and append everything on a DB
        await unitOfWork.RegisterTransaction(MapOcppTransaction(request));

        // check whether we have any missing transactions
        var latest = await unitOfWork.TryGetLatestTransaction(request.TransactionInfo.TransactionId);
        if (latest is not null && latest.SeqNo + 1 != request.SeqNo)
        {
            _logger.LogWarning($"{clientIdentifier}: Transaction {request.TransactionInfo.TransactionId} - Missing transaction update event. Latest SeqNo: {latest.SeqNo} Current SeqNo: {request.SeqNo}");

            /// TODO: Schedule a get transaction report or something
        }

        if (request.EventType == TransactionEventEnum.Ended)
        {
            // generate a snapshot of the transaction here
        }

        var response = new TransactionEventResponse();
        return new RpcResult()
        {
            Result = call.CreateResult(response),
            ReturnToCS = true
        };
    }
}
