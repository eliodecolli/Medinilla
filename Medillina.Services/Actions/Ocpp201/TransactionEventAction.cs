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
            total += meter.SampledValue.Select(m => m.Value * (decimal)Math.Pow(m.UnitOfMeasure.Multiplier, 10)).Sum();
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
            TriggerReason = Enum.GetName(request.TriggerReason) ?? "UNKNOWN"
        };
    }

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var request = call.As<TransactionEventRequest>();
        switch(request.EventType)
        {
            //case TransactionEventEnum.Updated:
            //    _logger.LogInformation($"{clientIdentifier}: Received transaction update status. Id={request.TransactionInfo.TransactionId} SeqNo={request.SeqNo}");
            //    break;
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
            var transaction = MapOcppTransaction(chargingStation, request);
            await unitOfWork.TransactionsSubUnit.RegisterTransaction(transaction);

            // check whether we have any missing transactions
            var currentTransactions = chargingStation.TransactionEvents is not null ? chargingStation.TransactionEvents.Where(t => t.TransactionId == request.TransactionInfo.TransactionId) : [];

            var latest = currentTransactions.LastOrDefault();
            if (latest is not null)
            {
                var diff = request.SeqNo - latest.SeqNo;
                if (diff > 1)
                {
                    _logger.LogWarning($"{clientIdentifier}: Transaction {request.TransactionInfo.TransactionId} - Missing transaction update event. Latest SeqNo: {latest.SeqNo} Current SeqNo: {request.SeqNo}");
                    /// TODO: Schedule a get transaction report or something
                }
                else if (diff == 0)
                {
                    _logger.LogWarning($"{clientIdentifier}: Transaction {request.TransactionInfo.TransactionId} repeated SeqNo={request.SeqNo}");
                    //await unitOfWork.Discard();
                }
            }

            if (request.EventType == TransactionEventEnum.Ended)
            {
                // generate a snapshot of the transaction here
                // calculate the total costs
                // send it back to the charging station
                var totalValueMetered = currentTransactions.Select(c => c.MeteredValue).Sum() + CalculateTotalMeteredValue(request.MeterValue);
                var unit = currentTransactions.Where(t => !string.IsNullOrEmpty(t.UnitName)).FirstOrDefault()?.UnitName ?? "";

                var connector = chargingStation.EvseConnectors?.Where(c => c.ConnectorId == (request.Evse?.ConnectorId ?? 1) && c.EvseId == (request.Evse?.Id ?? 1)).FirstOrDefault();
                
                var snapshot = new TransactionSnapshot()
                {
                    ChargingStationId = chargingStation.Id,
                    StartedAt = currentTransactions.FirstOrDefault()?.Timestamp ?? transaction.Timestamp,
                    EndedAt = transaction.Timestamp,
                    TotalMeteredValue = totalValueMetered,
                    TotalCost = CalculateTotalCosts(totalValueMetered, chargingStation, unit),
                    TokenId = request.IdToken?.Token ?? "",
                    EvseConnectorId = connector?.Id ?? null,
                    TransactionId = request.TransactionInfo.TransactionId,
                    Unit = unit,
                    StartReason = currentTransactions.FirstOrDefault()?.TriggerReason ?? "UNKNOWN",
                    EndReason = Enum.GetName(request.TriggerReason) ?? "UNKNOWN",
                };

                await unitOfWork.TransactionsSubUnit.RegisterFinalSnapshot(snapshot);
            }

            await unitOfWork.Save();

            var response = new TransactionEventResponse();
            return new RpcResult()
            {
                Result = call.CreateResult(response),
                ReturnToCS = true
            };
        }
    }
}
