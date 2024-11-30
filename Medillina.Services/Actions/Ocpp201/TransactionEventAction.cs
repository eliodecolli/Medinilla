using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.WAMP;
using Microsoft.Extensions.Logging;

using DbTransaction = Medinilla.DataAccess.Relational.Models.TransactionEvent;
using DbChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;

namespace Medinilla.Services.Actions.Ocpp201;

public sealed class TransactionEventAction(ILogger<TransactionEventAction> _logger, ChargingStationUnitOfWork unitOfWork)
    : IOcppAction
{
    // quick access
    private sealed class EventTypes
    {
        public static string Start = Enum.GetName(TransactionEventEnum.Started) ?? "Started";

        public static string End = Enum.GetName(TransactionEventEnum.Ended) ?? "Ended";

        public static string Update = Enum.GetName(TransactionEventEnum.Updated) ?? "Updated";
    }

    public string ActionName => "TransactionEvent";

    private string TryGetUnit(IEnumerable<MeterValue>? meters)
    {
        if (meters is null)
        {
            return "";
        }

        var meter = meters.FirstOrDefault();
        if (meter is null)
        {
            return "";
        }

        var sampled = meter.SampledValue.FirstOrDefault();
        if (sampled is null)
        {
            return "";
        }

        return sampled.UnitOfMeasure.Unit;
    }

    private decimal CalculateTotalMeteredValue(IEnumerable<MeterValue>? meters)
    {
        if (meters is null)
        {
            return 0.0M;
        }

        decimal total = 0.0M;
        foreach (var meter in meters)
        {
            total += meter.SampledValue.Select(m => m.Value * (decimal)Math.Pow(10, m.UnitOfMeasure.Multiplier)).Sum();
        }
        return total;
    }

    private decimal CalculateTotalCosts(decimal totalValue, DbChargingStation cs, string unit)
    {
        var unitPrice = !string.IsNullOrEmpty(unit) ? cs.Tariffs?.Where(t => t.UnitName == unit).FirstOrDefault()?.UnitPrice ?? 1.0M : 1.0M;
        return totalValue * unitPrice;
    }

    private DbTransaction MapOcppTransaction(DbChargingStation cs, TransactionEventRequest request)
    {
        return new DbTransaction()
        {
            TransactionId = request.TransactionInfo.TransactionId,
            SeqNo = request.SeqNo,
            Timestamp = request.Timestamp.ToUniversalTime(),
            IdToken = request.IdToken?.Token,
            EVSEId = request.Evse?.Id,
            Offline = request.Offline,
            ChargingStationId = cs.Id,
            MeteredValue = CalculateTotalMeteredValue(request.MeterValue),
            UnitName = TryGetUnit(request.MeterValue),
            TriggerReason = Enum.GetName(request.TriggerReason) ?? "UNKNOWN",
            EventType = Enum.GetName(request.EventType) ?? "UNKNOWN"
        };
    }

    private decimal GenerateTransactionSnapshot(DbChargingStation cs, IEnumerable<TransactionEvent> currentTransactions, DbTransaction tx, TransactionEventRequest request)
    {
        // generate a snapshot of the transaction here
        // calculate the total costs
        // send it back to the charging station
        var totalValueMetered = currentTransactions.Select(c => c.MeteredValue).Sum();
        var unit = currentTransactions.Where(t => !string.IsNullOrEmpty(t.UnitName)).FirstOrDefault()?.UnitName ?? "";

        var connector = cs.EvseConnectors?.Where(c => c.ConnectorId == (request.Evse?.ConnectorId ?? 1) && c.EvseId == (request.Evse?.Id ?? 1)).FirstOrDefault();

        var snapshot = new TransactionSnapshot()
        {
            ChargingStationId = cs.Id,
            StartedAt = currentTransactions.FirstOrDefault()?.Timestamp ?? tx.Timestamp,
            EndedAt = tx.Timestamp,
            TotalMeteredValue = totalValueMetered,
            TotalCost = CalculateTotalCosts(totalValueMetered, cs, unit),
            TokenId = request.IdToken?.Token ?? "",
            EvseConnectorId = connector?.Id ?? null,
            TransactionId = request.TransactionInfo.TransactionId,
            Unit = unit,
            StartReason = currentTransactions.FirstOrDefault()?.TriggerReason ?? "UNKNOWN",
            EndReason = Enum.GetName(request.TriggerReason) ?? "UNKNOWN",
        };

        cs.TransactionSnapshots.Add(snapshot);
        return snapshot.TotalCost;
    }

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var request = call.As<TransactionEventRequest>();
        switch(request.EventType)
        {
            case TransactionEventEnum.Started:
                _logger.LogInformation($"{clientIdentifier} started a new transaction. Reason: {request.TriggerReason}");
                break;
            case TransactionEventEnum.Ended:
                _logger.LogInformation($"{clientIdentifier} ended transaction with id {request.TransactionInfo.TransactionId}");
                break;
        }

        // TODO: Implement checks here and append everything on a DB
        var chargingStation = await unitOfWork.GetChargingStation(clientIdentifier);
        if (chargingStation == null)
        {
            _logger.LogError($"Invalid client identifier: {clientIdentifier}");
            return new RpcResult()
            {
                Error = call.CreateErrorResult<TransactionEventResponse>(OcppCallError.ErrorCodes.GenericError, $"Invalid client identifier: {clientIdentifier} not found."),
                ReturnToCS = true
            };
        }
        else
        {
            var response = new TransactionEventResponse();

            var transaction = MapOcppTransaction(chargingStation, request);
            if (transaction is null)
            {
                _logger.LogError($"{clientIdentifier}: Could not map incoming request: {call.Payload} to relational transaction type.");
                return new RpcResult()
                {
                    Error = OcppCallError.InternalError,
                };
            }
            else
            {
                var currentTransactions = chargingStation.TransactionEvents is not null ? chargingStation.TransactionEvents
                    .Where(t => t.TransactionId == request.TransactionInfo.TransactionId)
                    .OrderBy(t => t.SeqNo).AsEnumerable() : [];

                if (currentTransactions.Any(c => (c.SeqNo == request.SeqNo)))
                {
                    _logger.LogWarning($"{clientIdentifier}: Transaction {request.TransactionInfo.TransactionId} trying to send a duplicate of an old SeqNo={request.SeqNo}");

                    return new RpcResult()
                    {
                        Error = call.CreateErrorResult<TransactionEventResponse>(OcppCallError.ErrorCodes.PropertyConstraintViolation, $"Duplicate SeqNo='{request.SeqNo}' is not allowed.")
                    };
                }
                else if(currentTransactions.Any(c => (c.EventType != EventTypes.Update) && c.EventType == Enum.GetName(request.EventType)))
                {
                    _logger.LogWarning($"{clientIdentifier}: Transaction {request.TransactionInfo.TransactionId} is trying to send a duplicate event of type EventType='{Enum.GetName(request.EventType) ?? "<UNKNOWN>"}'.");
                    return new RpcResult()
                    {
                        Error = call.CreateErrorResult<TransactionEventResponse>(OcppCallError.ErrorCodes.OccurrenceConstraintViolation, $"Duplicate EventType='{Enum.GetName(request.EventType) ?? "<UNKNOWN>"}' is not allowed.")
                    };
                }
                else
                {
                    chargingStation.TransactionEvents.Add(transaction);

                    ////////////////////////////////////////////////
                    /// Don't do this right now.
                    /// 
                    // do we really wanna check this?
                    // my fear is that it might grow out exponentially once the system scales up a lot
                    //var missingTransactions = await unitOfWork.TransactionsSubUnit.TryGetMissingTransactionsForIncomingEvent(currentTransactions, request.SeqNo);
                    //if (missingTransactions.Length > 0)
                    //{
                    //    _logger.LogWarning($"{clientIdentifier}: Transaction {request.TransactionInfo.TransactionId} - Missing transaction(s) update event for {string.Join(";", missingTransactions.Select(x => "SeqNo=" + x))} Current SeqNo: {request.SeqNo}");
                    //    // TODO: Schedule a get transaction report or something
                    //}
                    /////////////////////////////////////////////////
                    
                    if (request.EventType == TransactionEventEnum.Ended)
                    {
                        // first check for sanity
                        if (currentTransactions.LastOrDefault()?.SeqNo > request.SeqNo)
                        {
                            // the END event cannot have a smaller SeqNo than the rest of them
                            await unitOfWork.Discard();
                            return new RpcResult()
                            {
                                Error = call.CreateErrorResult<TransactionEventResponse>(OcppCallError.ErrorCodes.OccurrenceConstraintViolation, $"Transaction {request.TransactionInfo.TransactionId} with EventType='Ended' must have greatest SeqNo than its previous siblings.")
                            };
                        }

                        response.TotalCost = GenerateTransactionSnapshot(chargingStation, currentTransactions, transaction, request);
                    }

                    await unitOfWork.Save();

                    return new RpcResult()
                    {
                        Result = call.CreateResult(response),
                        ReturnToCS = true
                    };
                }
            }
        }
    }
}
