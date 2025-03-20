using Medinilla.Core.Logic.Authorization;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;
using DbChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;
using DbTransaction = Medinilla.DataAccess.Relational.Models.TransactionEvent;
using IdTokenDb = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;

namespace Medinilla.Services.Actions.Ocpp201;

public sealed class TransactionEventAction(ILogger<TransactionEventAction> _logger,
    ChargingStationUnitOfWork unitOfWork,
    AuthorizationAlgorithmFactory authFactory)
    : IOcppAction
{
    // quick access
    private sealed class EventTypes
    {
        public static string Start = nameof(TransactionEventEnum.Started) ?? "Started";

        public static string End = nameof(TransactionEventEnum.Ended) ?? "Ended";

        public static string Update = nameof(TransactionEventEnum.Updated) ?? "Updated";
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

    private decimal ScaleToKW(SampledValue value)
    {
        if (value.UnitOfMeasure.Unit.ToLower() == "wh")
        {
            return value.Value / 1000;
        }
        else
        {
            return value.Value * (decimal)Math.Pow(10, value.UnitOfMeasure.Multiplier);
        }
    }

    private decimal CalculateTotalMeteredValue(TransactionEventRequest request)
    {
        var meters = request.MeterValue;
        if (meters is null)
        {
            return 0.0M;
        }

        decimal total = 0.0M;
        foreach (var meter in meters)
        {
            var sampledValues = meter.SampledValue.Where(s => s.Measurand == MeasurandEnum.EnergyActiveImportInterval || s.Measurand == MeasurandEnum.EnergyActiveImportRegister);
            total += sampledValues.Where(t => t.Measurand == MeasurandEnum.EnergyActiveImportInterval).Select(ScaleToKW).Sum();
        }
        return total;
    }

    private decimal CalculateTotalCosts(decimal totalValue, DbChargingStation cs, string unit)
    {
        var unitPrice = !string.IsNullOrEmpty(unit) ? cs.Tariffs?.Where(t => t.UnitName == unit).FirstOrDefault()?.UnitPrice ?? 1.0M : 1.0M;
        var total = totalValue * unitPrice;
        return total;
    }

    private decimal? TryGetTotalEnergyImport(IEnumerable<SampledValue> sampled)
    {
        var result = sampled.Where(s => s.Phase is null).FirstOrDefault();
        if (result is not null)
        {
            return result.Value;
        }
        else
        {
            // apparently we have phases?
            var phasesSamples = sampled.Where(s => s.Phase is not null);
            if (phasesSamples.Count() > 0)
            {
                // we have multiple phases
                // let's sum them all
                return phasesSamples.Select(s => s.Value).Sum();
            }
            else
            {
                // wat?
                return null;
            }
        }
    }

    private decimal GetTotalEnergyImport(IEnumerable<TransactionEvent> transactions, TransactionEventRequest lastRequest)
    {
        var lastMetered = lastRequest.MeterValue?.OrderByDescending(m => m.Timestamp).FirstOrDefault();
        var firstMetered = lastRequest.MeterValue?.OrderBy(m => m.Timestamp).FirstOrDefault();

        // try to get the last metered value
        // then check if there's an Energy.Active.Import.Register payload
        // if there is, check if it's not phase-dependant, otherwise sum all of the phases
        if (lastMetered is not null)
        {
            var energyActiveImport = lastMetered.SampledValue.Where(s => s.Measurand == MeasurandEnum.EnergyActiveImportRegister);
            if (energyActiveImport.Count() > 1)
            {
                // apparently there are multiple phases
                // before we do that let's check if there's a non-phase-dependent value
                var result = TryGetTotalEnergyImport(energyActiveImport);
                if (result is not null)
                {
                    return result.Value;  // not sure why without .Value it's not working
                }
            }
            else
            {
                var firstResult = energyActiveImport.FirstOrDefault()?.Value;
                if (firstResult is not null)
                {
                    return firstResult.Value;
                }
            }
        }
        return transactions.Select(t => t.MeteredValue).Sum();
    }

    private async Task<string> PerformAuthorization(DbChargingStation cs, TransactionEventRequest request, AuthorizationContext context)
    {
        var status = await authFactory.RunAuthorization(request.IdToken, context);

        if (status == AuthorizeStatus.Accepted)
        {
            status = request.EventType == TransactionEventEnum.Started &&
                context.IdToken is not null &&
                context.IdToken.IsUnderTx ?
                AuthorizeStatus.ConcurrentTx :
                AuthorizeStatus.Accepted;
        }

        return status;
    }

    private bool PerformTokenCleanUp(DbChargingStation cs, IdToken token)
    {
        var contextIdToken = cs.IdTokens.FirstOrDefault(t => t.Token == token.Token);
        if (contextIdToken is not null)
        {
            return cs.IdTokens.Remove(contextIdToken);
        }

        return true;
    }

    private DbTransaction MapOcppTransaction(DbChargingStation cs, TransactionEventRequest request)
    {
        return new DbTransaction()
        {
            TransactionId = request.TransactionInfo.TransactionId,
            SeqNo = request.SeqNo,
            Timestamp = request.Timestamp.ToUniversalTime(),
            EVSEId = request.Evse?.Id,
            Offline = request.Offline,
            ChargingStationId = cs.Id,
            MeteredValue = CalculateTotalMeteredValue(request),
            UnitName = TryGetUnit(request.MeterValue),
            TriggerReason = Enum.GetName(request.TriggerReason) ?? "UNKNOWN",
            EventType = Enum.GetName(request.EventType) ?? "UNKNOWN"
        };
    }

    private decimal GenerateTransactionSnapshot(DbChargingStation cs,
        IEnumerable<TransactionEvent> currentTransactions,
        DbTransaction finalTx,
        IdTokenDb? idToken,
        TransactionEventRequest request)
    {
        // generate a snapshot of the transaction here
        // calculate the total costs
        // send it back to the charging station
        var totalValueMetered = GetTotalEnergyImport(currentTransactions, request);
        var unit = currentTransactions.Where(t => !string.IsNullOrEmpty(t.UnitName)).FirstOrDefault()?.UnitName ?? "";

        var connector = cs.EvseConnectors?.Where(c => c.ConnectorId == (request.Evse?.ConnectorId ?? 1) && c.EvseId == (request.Evse?.Id ?? 1)).FirstOrDefault();

        var snapshot = new TransactionSnapshot()
        {
            ChargingStationId = cs.Id,
            StartedAt = currentTransactions.FirstOrDefault()?.Timestamp ?? finalTx.Timestamp,
            EndedAt = finalTx.Timestamp,
            TotalMeteredValue = totalValueMetered,
            TotalCost = CalculateTotalCosts(totalValueMetered, cs, unit),
            EvseConnectorId = connector?.Id ?? null,
            TransactionId = request.TransactionInfo.TransactionId,
            Unit = unit,
            StartReason = currentTransactions.FirstOrDefault()?.TriggerReason ?? "UNKNOWN",
            EndReason = Enum.GetName(request.TriggerReason) ?? "UNKNOWN",
            IdTokenId = idToken?.Id,
        };

        cs.TransactionSnapshots.Add(snapshot);
        return snapshot.TotalCost;
    }

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var request = call.As<TransactionEventRequest>();

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
            if (transaction is null)
            {
                _logger.LogError($"{clientIdentifier}: Could not map incoming request: {call.Payload} to relational transaction type.");
                return new RpcResult()
                {
                    Error = call.CreateErrorResult<TransactionEventResponse>(OcppCallError.ErrorCodes.InternalError, "Could not map Transaction Event."),
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
                else if (currentTransactions.Any(c => (c.EventType != EventTypes.Update) && c.EventType == Enum.GetName(request.EventType)))
                {
                    _logger.LogWarning($"{clientIdentifier}: Transaction {request.TransactionInfo.TransactionId} is trying to send a duplicate event of type EventType='{Enum.GetName(request.EventType) ?? "<UNKNOWN>"}'.");
                    return new RpcResult()
                    {
                        Error = call.CreateErrorResult<TransactionEventResponse>(OcppCallError.ErrorCodes.OccurrenceConstraintViolation, $"Duplicate EventType='{Enum.GetName(request.EventType) ?? "<UNKNOWN>"}' is not allowed.")
                    };
                }
                else
                {
                    var context = AuthUtils.GenerateAuthContext(chargingStation, request.Evse?.Id, true);
                    var authStatus = await PerformAuthorization(chargingStation, request, context);

                    var response = new TransactionEventResponse()
                    {
                        IdTokenInfo = new IdTokenInfo()
                        {
                            Status = authStatus,
                        }
                    };

                    if (authStatus != AuthorizeStatus.Accepted)
                    {
                        return new RpcResult()
                        {
                            Result = call.CreateResult(response)
                        };
                    }

                    await unitOfWork.TransactionsSubUnit.RegisterTransaction(transaction, context.IdToken);

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

                        response.TotalCost = GenerateTransactionSnapshot(chargingStation, currentTransactions, transaction, context.IdToken, request);

                        if (request.IdToken?.Type == IdTokenType.Central)
                        {
                            if (PerformTokenCleanUp(chargingStation, request.IdToken))
                            {
                                _logger.LogInformation($"{clientIdentifier}: Removed temporary token {request.IdToken.Token} because transaction is done.");
                            }
                            else
                            {
                                _logger.LogWarning($"{clientIdentifier}: Temp token {request.IdToken.Token} couldn't be removed.");
                            }
                        }
                    }

                    switch (request.EventType)
                    {
                        case TransactionEventEnum.Started:
                            _logger.LogInformation($"{clientIdentifier} started a new transaction. Reason: {request.TriggerReason}");
                            break;
                        case TransactionEventEnum.Ended:
                            _logger.LogInformation($"{clientIdentifier} ended transaction with id {request.TransactionInfo.TransactionId}");
                            break;
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
