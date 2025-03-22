using Medinilla.Core.Logic.Authorization;
using Medinilla.Core.Logic.Transactions;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;
using DbChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;
using DbTransaction = Medinilla.DataAccess.Relational.Models.TransactionEvent;
using IdTokenDb = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;
using ConsumptionTypeDb = Medinilla.DataAccess.Relational.Enums.ConsumptionType;
using Medinilla.DataTypes.Core;

namespace Medinilla.Services.Actions.Ocpp201;

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

    private decimal CalculateTotalCosts(decimal totalValue, DbChargingStation cs, string unit)
    {
        var unitPrice = !string.IsNullOrEmpty(unit) ? cs.Tariffs?.Where(t => t.UnitName == unit).FirstOrDefault()?.UnitPrice ?? 1.0M : 1.0M;
        var total = totalValue * unitPrice;
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

    private DbTransaction MapOcppTransaction(DbChargingStation cs, TransactionEventRequest request)
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
            EventType = Enum.GetName(request.EventType) ?? "UNKNOWN"
        };
    }

    private TransactionConsumption? GetTransactionConsumption(TransactionEventRequest request, TransactionEvent transaction, TransactionSnapshot snapshot)
    {
        return request.MeterValue is not null ? transactionService.GetTransactionConsumption(request.MeterValue, snapshot.LastEvent) : null;
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

                    var snapshot = await unitOfWork.TransactionSubUnit.GetOrCreateSnapshot(transaction);

                    var consumption = GetTransactionConsumption(request, transaction, snapshot);

                    transaction.TotalConsuption = consumption?.Consumption ?? 0.0M;
                    transaction.ConsumptionType = (ConsumptionTypeDb?)consumption?.ConsumptionType;

                    await unitOfWork.TransactionSubUnit.RegisterTransaction(transaction, context.IdToken);

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

                        snapshot = await unitOfWork.TransactionSubUnit.FinalizeSnapshot(transaction, snapshot, consumption);
                        var unitName = await unitOfWork.TransactionSubUnit.GetTransactionUnit(transaction.TransactionId);

                        response.TotalCost = CalculateTotalCosts(snapshot.TotalMeteredValue, chargingStation, unitName ?? "UNKNOWN");

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
                    else
                    {
                        await unitOfWork.TransactionSubUnit.UpdateSnapshot(transaction, consumption, snapshot);
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
