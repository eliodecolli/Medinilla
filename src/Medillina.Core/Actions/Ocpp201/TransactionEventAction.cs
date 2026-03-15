using Medinilla.Core.Logic.Authorization;
using Medinilla.Core.v1.Transactions;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;
using ConsumptionTypeDb = Medinilla.DataAccess.Relational.Enums.ConsumptionType;
using DbChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;
using DbTransaction = Medinilla.DataAccess.Relational.Models.TransactionEvent;
using IdTokenDb = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;

namespace Medinilla.Core.Actions.Ocpp201;

public sealed class TransactionEventAction(ILogger<TransactionEventAction> _logger,
    ChargingStationUnitOfWork unitOfWork,
    AuthorizationAlgorithmFactory authFactory,
    TransactionService transactionService)
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

    private decimal CalculateTotalCosts(float totalValue, DbChargingStation cs, string unit)
    {
        var unitPrice = !string.IsNullOrEmpty(unit) ? cs.Tariffs?.Where(t => t.UnitName == unit).FirstOrDefault()?.UnitPrice ?? 1.0M : 1.0M;
        var total = Convert.ToDecimal(totalValue) * unitPrice;
        return total;
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

    private DbTransaction MapOcppTransaction(DbChargingStation cs, TransactionEventRequest request, IdTokenDb? idToken)
    {
        var unitName = request.MeterValue?.SelectMany(c => c.SampledValue).FirstOrDefault(s => s.Measurand == MeasurandEnum.EnergyActiveImportRegister ||
                                                                                               s.Measurand == MeasurandEnum.EnergyActiveImportInterval)?.UnitOfMeasure?.Unit ?? "UNKNOWN";

        return new DbTransaction()
        {
            TransactionId = request.TransactionInfo.TransactionId,
            SeqNo = request.SeqNo,
            Timestamp = request.Timestamp.ToUniversalTime(),
            EVSEId = request.Evse?.Id,
            Offline = request.Offline,
            ChargingStationId = cs.Id,
            UnitName = unitName,
            TriggerReason = Enum.GetName(request.TriggerReason) ?? "UNKNOWN",
            EventType = Enum.GetName(request.EventType) ?? "UNKNOWN",
            IdTokenId = idToken?.Id,
        };
    }

    private TransactionConsumption? GetTransactionConsumption(TransactionEventRequest request, TransactionEvent transaction)
    {
        return transactionService.GetTransactionConsumption(request.MeterValue);
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

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
#if DEBUG
        SaveTxLocally(call);
#endif
        _logger.LogInformation($"Received Transaction event from {clientIdentifier}");
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
            var idToken = await unitOfWork.TryGetIdToken(request.TransactionInfo.TransactionId, request.IdToken?.Token ?? "");
            if (idToken is null)
            {
                _logger.LogWarning($"IdToken for transaction {request.TransactionInfo.TransactionId}, event '{Enum.GetName(request.EventType)}' not found ('{request.IdToken?.Token}' was found in request)");
            }

            var transaction = MapOcppTransaction(chargingStation, request, idToken);

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

                var currentTransactions = chargingStation.TransactionEvents is not null ? chargingStation.TransactionEvents
                    .Where(t => t.TransactionId == request.TransactionInfo.TransactionId)
                    .OrderBy(t => t.SeqNo).ToArray() : [];

                if (currentTransactions.Any(c => c.SeqNo == request.SeqNo))
                {
                    _logger.LogWarning($"{clientIdentifier}: Transaction {request.TransactionInfo.TransactionId} trying to send a duplicate of an old SeqNo={request.SeqNo}");

                    return new RpcResult()
                    {
                        Result = call.CreateResult(response),
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
                    var consumption = GetTransactionConsumption(request, transaction);

                    transaction.TotalConsuption = Convert.ToDecimal(consumption?.Consumption ?? 0.0);
                    transaction.ConsumptionType = (ConsumptionTypeDb?)consumption?.ConsumptionType;

                    transaction = await unitOfWork.TransactionSubUnit.RegisterTransaction(transaction, context.IdToken);

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

                        // do we need to keep a coherent snapshot after each tx event?
                        if (currentTransactions.Length > 0)
                        {
                            var firstTransaction = currentTransactions.FirstOrDefault(tx => tx.EventType == Enum.GetName(TransactionEventEnum.Started));
                            if (firstTransaction is null)
                            {
                                _logger.LogWarning($"Partially finalizing tx snapshot for {request.TransactionInfo.TransactionId}: No 'Started' event was found.");
                            }
                            await unitOfWork.TransactionSubUnit.FinalizeSnapshot(firstTransaction, transaction, consumption);
                        }

                        var unitName = await unitOfWork.TransactionSubUnit.GetTransactionUnit(transaction.TransactionId);

                        response.TotalCost = CalculateTotalCosts(consumption?.Consumption ?? 0.0f, chargingStation, unitName ?? "UNKNOWN");

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

                        if (idToken is not null)
                        {
                            await unitOfWork.ReleaseToken(idToken);
                            _logger.LogInformation($"Released token {idToken.Token}");
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
