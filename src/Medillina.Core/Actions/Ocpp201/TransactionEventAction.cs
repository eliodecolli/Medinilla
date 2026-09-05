using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.Logic.Authorization;
using Medinilla.Core.Logic.Exceptions;
using Medinilla.Core.v1.Transactions;
using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;
using ChargingStationDb = Medinilla.DataAccess.Relational.Models.ChargingStation;
using ConsumptionTypeDb = Medinilla.DataAccess.Relational.Enums.ConsumptionType;
using DbTransaction = Medinilla.DataAccess.Relational.Models.TransactionEvent;
using IdTokenDb = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;

namespace Medinilla.Core.Actions.Ocpp201;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class TransactionEventAction(
    ILogger<TransactionEventAction> logger,
    IChargerService chargerService,
    IUnitOfWorkWrapper unitOfWorkWrapper,
    AuthorizationAlgorithmFactory authFactory,
    ConsumptionService consumptionService,
    ITariffService tariffService,
    IIdTokenService idTokenService,
    ITransactionService transactionService)
    : IOcppAction
{
    public string ActionName => OcppActionNames.TransactionEvent;

    private decimal? GetPhaseConsumption(float?[]? phases, int index)
    {
        if (phases is null) return null;
        var phase = phases[index];
        return phase.HasValue ? Convert.ToDecimal(phase) : null;
    }

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        logger.LogInformation($"Received Transaction event from {clientIdentifier}");
        var request = call.As<TransactionEventRequest>();

        try
        {
            var chargingStation = await chargerService.GetByClientIdentifier(clientIdentifier);
            var requestIdToken = request.IdToken?.Token;
            var currentTransactions = chargingStation.TransactionEvents
                .Where(t => t.TransactionId == request.TransactionInfo.TransactionId)
                .OrderBy(t => t.SeqNo).ToList();
            
            var idToken = idTokenService.TryGetForTransaction(chargingStation, currentTransactions,
                requestIdToken);
            
            var response = new TransactionEventResponse();
            var context = AuthUtils.GenerateAuthContext(chargingStation, request.Evse?.Id, idToken);
            var authStatus = await PerformAuthorization(request, context);
            
            response.IdTokenInfo = new IdTokenInfo()
            {
                Status = authStatus,
            };

            if (authStatus != AuthorizeStatus.Accepted)
            {
                logger.LogError("[Transaction Event]: [{event}]: Authorization failed with status='{status}'",
                    request.EventType.ToString(), authStatus);
                
                if (request.EventType == TransactionEventEnum.Started)
                {
                    // block the transaction from starting if something failed
                    return new RpcResult()
                    {
                        Result = call.CreateResult(response)
                    };
                }
                
                return new RpcResult()
                {
                    Error = call.CreateErrorResult<TransactionEventResponse>(OcppCallError.ErrorCodes.InternalError,
                        $"Authorization failed with status='{authStatus}'")
                };
            }
            
            // setting skipIfNullToken=false will short circuit the flow before we reach this point
            var transaction = MapOcppTransaction(chargingStation, request, idToken!);

            var validationFailure =
                ValidateTransactionEvent(request, currentTransactions, response, call, clientIdentifier);
            if (validationFailure is not null)
            {
                return validationFailure;
            }
            else
            {
                var consumption = GetTransactionConsumption(request);
                
                #if DEBUG
                if (consumption.Consumption < 0)
                {
                    logger.LogDebug("Transaction {TransactionId} consumption is negative", request.TransactionInfo.TransactionId);
                    
                    // dump the event payload
                    SaveTxLocally(call);
                }
                #endif

                transaction.RegisterValue = Convert.ToDecimal(consumption.Consumption);
                transaction.PhaseOneValue = GetPhaseConsumption(consumption.PhaseConsumption, 0);
                transaction.PhaseTwoValue = GetPhaseConsumption(consumption.PhaseConsumption, 1);
                transaction.PhaseThreeValue = GetPhaseConsumption(consumption.PhaseConsumption, 2);

                transaction.ConsumptionType = (ConsumptionTypeDb?)consumption.ConsumptionType;

                transactionService.RegisterTransaction(chargingStation, transaction, idToken);

                if (request.EventType == TransactionEventEnum.Ended)
                {
                    var lastSeqNo = currentTransactions.LastOrDefault()?.SeqNo;
                    // first check for sanity
                    if (lastSeqNo > request.SeqNo)
                    {
                        // the END event cannot have a smaller SeqNo than the rest of them :(
                        logger.LogError("Transaction for clientIdentifier='{clientIdentifier}' seqNo={seqNo} is smaller than last seqNo={seqNoLast} ",
                            clientIdentifier, request.SeqNo, lastSeqNo);
                        
                        // cleanup db context just in case
                        // returning here will discard the changes automatically,
                        // but this still feels like good practice
                        unitOfWorkWrapper.DiscardChanges();
                        return new RpcResult()
                        {
                            Error = call.CreateErrorResult<TransactionEventResponse>(
                                OcppCallError.ErrorCodes.OccurrenceConstraintViolation,
                                $"Transaction {request.TransactionInfo.TransactionId} with EventType='Ended' must have greatest SeqNo than its previous siblings.")
                        };
                    }

                    // do we need to keep a coherent snapshot after each tx event?
                    if (currentTransactions.Count > 0)
                    {
                        var firstTransaction = currentTransactions.FirstOrDefault(tx =>
                            tx.EventType == nameof(TransactionEventEnum.Started));
                        if (firstTransaction is null)
                        {
                            logger.LogError(
                                "Partially finalizing tx snapshot for {TransactionInfoTransactionId}: No 'Started' event was found.",
                                request.TransactionInfo.TransactionId);
                        }
                        
                        var unitName =
                            await transactionService.GetTransactionUnit(chargingStation, transaction.TransactionId);

                        var totalCost = tariffService.CalculateTotalCosts(consumption.Consumption,
                            chargingStation, unitName ?? "UNKNOWN");
                        
                        // send the final response cost to the charger
                        response.TotalCost = totalCost;

                        var snapshot = new TransactionSnapshot
                        {
                            ChargingStationId = transaction.ChargingStationId,
                            TransactionId = transaction.TransactionId,
                            StartReason = firstTransaction?.TriggerReason ??
                                          Enum.GetName(TriggerReasonEnum.AbnormalCondition)!,
                            EndedAt = transaction.Timestamp,
                            EndReason = transaction.TriggerReason,
                            TotalCost = totalCost,
                        };

                        try
                        {
                            transactionService.FinalizeTransaction(chargingStation, snapshot);
                        }
                        catch (TransactionException e)
                        {
                            logger.LogError(
                                "Cannot finalize transaction for ci='{ci}': Transaction with id='{tx}' has already been finalized.",
                                clientIdentifier, snapshot.TransactionId);
                        }
                    }

                    idToken.IsUnderTx = false;
                    logger.LogInformation("Released token for tx={tx} ci={ci}",
                        transaction.Id, clientIdentifier);
                        
                    if (request.IdToken?.Type == IdTokenType.Central)
                    {
                        if (idTokenService.RemoveTemporaryToken(chargingStation, idToken))
                        {
                            logger.LogInformation(
                                "{ClientIdentifier}: Removed temporary token {IdTokenToken} because transaction is done.", clientIdentifier, request.IdToken.Token);
                        }
                        else
                        {
                            logger.LogWarning(
                                "{ClientIdentifier}: Temp token {IdTokenToken} couldn't be removed.", clientIdentifier, request.IdToken.Token);
                        }
                    }
                }

                switch (request.EventType)
                {
                    case TransactionEventEnum.Started:
                        logger.LogInformation(
                            $"{clientIdentifier} started a new transaction. Reason: {request.TriggerReason}");
                        break;
                    case TransactionEventEnum.Ended:
                        logger.LogInformation(
                            $"{clientIdentifier} ended transaction with id {request.TransactionInfo.TransactionId} Total Cost: {response.TotalCost}");
                        break;
                }

                await unitOfWorkWrapper.SaveChanges();

                return new RpcResult()
                {
                    Result = call.CreateResult(response),
                    ReturnToCS = true
                };
            }
        }
        catch (AggregateRootNotFoundException)
        {
            logger.LogError($"Invalid client identifier: {clientIdentifier}");
            return new RpcResult()
            {
                Error = call.CreateErrorResult<TransactionEventResponse>(OcppCallError.ErrorCodes.GenericError,
                    $"Invalid client identifier: {clientIdentifier} not found."),
                ReturnToCS = true
            };
        }
    }

    // TODO: https://github.com/eliodecolli/Medinilla/issues/51
    private async Task<string> PerformAuthorization(TransactionEventRequest request,
        AuthorizationContext context)
    {
        var status = await authFactory.RunAuthorization(context);

        if (status == AuthorizeStatus.Accepted)
        {
            status = request.EventType == TransactionEventEnum.Started &&
                     context.IdToken is not null &&
                     context.IdToken.IsUnderTx
                ? AuthorizeStatus.ConcurrentTx
                : AuthorizeStatus.Accepted;
        }

        return status;
    }

    private DbTransaction MapOcppTransaction(ChargingStationDb chargingStation,
        TransactionEventRequest request,
        IdTokenDb idToken)
    {
        var unitName = request.MeterValue?.SelectMany(c => c.SampledValue).FirstOrDefault(s =>
            s.Measurand is MeasurandEnum.EnergyActiveImportRegister or MeasurandEnum.EnergyActiveImportInterval)?.UnitOfMeasure.Unit ?? "UNKNOWN";
        
        return new DbTransaction()
        {
            TransactionId = request.TransactionInfo.TransactionId,
            SeqNo = request.SeqNo,
            Timestamp = request.Timestamp.ToUniversalTime(),
            EVSEId = request.Evse?.Id,
            Offline = request.Offline,
            ChargingStationId = chargingStation.Id,
            UnitName = unitName,
            TriggerReason = Enum.GetName(request.TriggerReason) ?? "UNKNOWN",
            EventType = request.EventType.ToString(),
            IdToken = idToken,
        };
    }

    private RpcResult? ValidateTransactionEvent(
        TransactionEventRequest request,
        IEnumerable<DbTransaction> currentTransactions,
        TransactionEventResponse response,
        OcppCallRequest call,
        string clientIdentifier)
    {
        if (currentTransactions.Any(c => c.SeqNo == request.SeqNo))
        {
            logger.LogWarning(
                $"{clientIdentifier}: Transaction {request.TransactionInfo.TransactionId} trying to send a duplicate of an old SeqNo={request.SeqNo}");

            return new RpcResult()
            {
                Result = call.CreateResult(response),
            };
        }

        if (currentTransactions.Any(c =>
                c.EventType != nameof(TransactionEventEnum.Updated) && c.EventType == Enum.GetName(request.EventType)))
        {
            logger.LogWarning(
                $"{clientIdentifier}: Transaction {request.TransactionInfo.TransactionId} is trying to send a duplicate event of type EventType='{Enum.GetName(request.EventType) ?? "<UNKNOWN>"}'.");
            return new RpcResult()
            {
                Error = call.CreateErrorResult<TransactionEventResponse>(
                    OcppCallError.ErrorCodes.OccurrenceConstraintViolation,
                    $"Duplicate EventType='{Enum.GetName(request.EventType) ?? "<UNKNOWN>"}' is not allowed.")
            };
        }

        return null;
    }

    private TransactionConsumption GetTransactionConsumption(TransactionEventRequest request)
    {
        return consumptionService.GetTransactionConsumption(request.MeterValue);
    }

#if DEBUG
    private void SaveTxLocally(OcppCallRequest req)
    {
        if (!Directory.Exists("transactions"))
        {
            Directory.CreateDirectory("transactions");
        }

        var path = Path.Combine("transactions", $"{req.MessageId}.json");
        File.WriteAllText(path, req.Payload);
    }
#endif
}