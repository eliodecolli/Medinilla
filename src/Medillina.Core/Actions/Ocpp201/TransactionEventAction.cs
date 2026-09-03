using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.Logic.Authorization;
using Medinilla.Core.v1.Transactions;
using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;
using ConsumptionTypeDb = Medinilla.DataAccess.Relational.Enums.ConsumptionType;
using DbTransaction = Medinilla.DataAccess.Relational.Models.TransactionEvent;
using IdTokenDb = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;

namespace Medinilla.Core.Actions.Ocpp201;

public sealed class TransactionEventAction(
    ILogger<TransactionEventAction> _logger,
    ChargingStationUnitOfWork unitOfWork,
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
#if DEBUG
        SaveTxLocally(call);
#endif
        _logger.LogInformation($"Received Transaction event from {clientIdentifier}");
        var request = call.As<TransactionEventRequest>();

        try
        {
            await unitOfWork.Start(c => c.ClientIdentifier == clientIdentifier);
        }
        catch (AggregateRootNotFoundException)
        {
            _logger.LogError($"Invalid client identifier: {clientIdentifier}");
            return new RpcResult()
            {
                Error = call.CreateErrorResult<TransactionEventResponse>(OcppCallError.ErrorCodes.GenericError,
                    $"Invalid client identifier: {clientIdentifier} not found."),
                ReturnToCS = true
            };
        }
        var requestIdToken = request.IdToken?.Token;
        var idToken = !string.IsNullOrEmpty(requestIdToken)
            ? await idTokenService.TryGetForTransaction(clientIdentifier, request.TransactionInfo.TransactionId)
            : null;
            
        var response = new TransactionEventResponse();
            
        if (idToken is null)
        {
            _logger.LogWarning(
                $"IdToken for transaction {request.TransactionInfo.TransactionId}, event '{Enum.GetName(request.EventType)}' not found ('{request.IdToken?.Token}' was found in request)");
        }
            
        var context = AuthUtils.GenerateAuthContext(unitOfWork.AggregateRoot, request.Evse?.Id, true);
        var authStatus = await PerformAuthorization(request, context);
            
        // TODO: Update this. "request" might not contain an Id Token on "UPDATE" events;
        // however we already know the related token to this transaction from the "START" event.
        if (idToken is not null)
        {
            response.IdTokenInfo = new IdTokenInfo()
            {
                Status = authStatus,
            };
        }

        if (authStatus != AuthorizeStatus.Accepted)
        {
            _logger.LogError("[Transaction Event]: [{event}]: Authorization failed with status='{status}'",
                request.EventType.ToString(), authStatus);
            return new RpcResult()
            {
                Result = call.CreateResult(response)
            };
        }

        var transaction = MapOcppTransaction(request, idToken);

        var currentTransactions = unitOfWork.AggregateRoot.TransactionEvents is not null
            ? unitOfWork.AggregateRoot.TransactionEvents
                .Where(t => t.TransactionId == request.TransactionInfo.TransactionId)
                .OrderBy(t => t.SeqNo).ToArray()
            : [];

        var validationFailure =
            ValidateTransactionEvent(request, currentTransactions, response, call, clientIdentifier);
        if (validationFailure is not null)
        {
            return validationFailure;
        }
        else
        {
            var consumption = GetTransactionConsumption(request);

            transaction.RegisterValue = Convert.ToDecimal(consumption?.Consumption ?? 0.0);
            transaction.PhaseOneValue = GetPhaseConsumption(consumption?.PhaseConsumption, 0);
            transaction.PhaseTwoValue = GetPhaseConsumption(consumption?.PhaseConsumption, 1);
            transaction.PhaseThreeValue = GetPhaseConsumption(consumption?.PhaseConsumption, 2);

            transaction.ConsumptionType = (ConsumptionTypeDb?)consumption?.ConsumptionType;

            transactionService.RegisterTransaction(unitOfWork.AggregateRoot, transaction, context.IdToken);

            if (request.EventType == TransactionEventEnum.Ended)
            {
                // first check for sanity
                if (currentTransactions.LastOrDefault()?.SeqNo > request.SeqNo)
                {
                    // the END event cannot have a smaller SeqNo than the rest of them
                    await unitOfWork.Discard();
                    return new RpcResult()
                    {
                        Error = call.CreateErrorResult<TransactionEventResponse>(
                            OcppCallError.ErrorCodes.OccurrenceConstraintViolation,
                            $"Transaction {request.TransactionInfo.TransactionId} with EventType='Ended' must have greatest SeqNo than its previous siblings.")
                    };
                }

                // do we need to keep a coherent snapshot after each tx event?
                if (currentTransactions.Length > 0)
                {
                    var firstTransaction = currentTransactions.FirstOrDefault(tx =>
                        tx.EventType == TransactionEventEnum.Started.ToString());
                    if (firstTransaction is null)
                    {
                        _logger.LogWarning(
                            $"Partially finalizing tx snapshot for {request.TransactionInfo.TransactionId}: No 'Started' event was found.");
                    }

                    var snapshot = new TransactionSnapshot
                    {
                        ChargingStationId = transaction.ChargingStationId,
                        TransactionId = transaction.TransactionId,
                        StartReason = firstTransaction?.TriggerReason ?? Enum.GetName(TriggerReasonEnum.AbnormalCondition)!,
                        EndedAt = transaction.Timestamp,
                        EndReason = transaction.TriggerReason,
                        TotalCost = Convert.ToDecimal(consumption?.Consumption ?? 0)
                    };
                    
                    await unitOfWork.TransactionSnapshots.AddAsync(snapshot);
                }

                var unitName = await transactionService.GetTransactionUnit(unitOfWork.AggregateRoot, transaction.TransactionId);

                response.TotalCost = tariffService.CalculateTotalCosts(consumption?.Consumption ?? 0.0f,
                    unitOfWork.AggregateRoot, unitName ?? "UNKNOWN");

                if (request.IdToken?.Type == IdTokenType.Central)
                {
                    if (idTokenService.RemoveTemporaryToken(unitOfWork.AggregateRoot, request.IdToken))
                    {
                        _logger.LogInformation(
                            $"{clientIdentifier}: Removed temporary token {request.IdToken.Token} because transaction is done.");
                    }
                    else
                    {
                        _logger.LogWarning(
                            $"{clientIdentifier}: Temp token {request.IdToken.Token} couldn't be removed.");
                    }
                }

                if (idToken is null)
                {
                    // try to find the active id token for this session
                    var token = unitOfWork.AggregateRoot.TransactionEvents?
                        .FirstOrDefault(t => t.TransactionId == transaction.TransactionId && 
                                             t.IdToken is not null)?.IdToken;
                    
                    if (token is not null)
                    {
                        if (token.IsUnderTx)
                        {
                            idToken = token;
                        }
                        else
                        {
                            _logger.LogError("Found token={token} for related to tx={tx} under ci={ci}, however it is not marked as under transaction.",
                                token.Token, transaction.Id, clientIdentifier);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Cannot find active token for ci={ci} under tx={tx}",
                            clientIdentifier, transaction.Id);
                    }
                }
                    
                // TODO: ew refactor maybe?
                if (idToken is not null)
                {
                    idToken.IsUnderTx = false;
                    _logger.LogInformation("Released token for tx={tx} ci={ci}",
                        transaction.Id, clientIdentifier);
                }
            }

            switch (request.EventType)
            {
                case TransactionEventEnum.Started:
                    _logger.LogInformation(
                        $"{clientIdentifier} started a new transaction. Reason: {request.TriggerReason}");
                    break;
                case TransactionEventEnum.Ended:
                    _logger.LogInformation(
                        $"{clientIdentifier} ended transaction with id {request.TransactionInfo.TransactionId} Total Cost: {response.TotalCost}");
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

    private async Task<string> PerformAuthorization(TransactionEventRequest request,
        AuthorizationContext context)
    {
        var status = await authFactory.RunAuthorization(request.IdToken, context);

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

    private DbTransaction MapOcppTransaction(TransactionEventRequest request, IdTokenDb? idToken)
    {
        var unitName = request.MeterValue?.SelectMany(c => c.SampledValue).FirstOrDefault(s =>
            s.Measurand == MeasurandEnum.EnergyActiveImportRegister ||
            s.Measurand == MeasurandEnum.EnergyActiveImportInterval)?.UnitOfMeasure?.Unit ?? "UNKNOWN";

        return new DbTransaction()
        {
            TransactionId = request.TransactionInfo.TransactionId,
            SeqNo = request.SeqNo,
            Timestamp = request.Timestamp.ToUniversalTime(),
            EVSEId = request.Evse?.Id,
            Offline = request.Offline,
            ChargingStationId = unitOfWork.AggregateRoot.Id,
            UnitName = unitName,
            TriggerReason = Enum.GetName(request.TriggerReason) ?? "UNKNOWN",
            EventType = request.EventType.ToString(),
            IdTokenId = idToken?.Id,
        };
    }

    private RpcResult? ValidateTransactionEvent(
        TransactionEventRequest request,
        DbTransaction[] currentTransactions,
        TransactionEventResponse response,
        OcppCallRequest call,
        string clientIdentifier)
    {
        if (currentTransactions.Any(c => c.SeqNo == request.SeqNo))
        {
            _logger.LogWarning(
                $"{clientIdentifier}: Transaction {request.TransactionInfo.TransactionId} trying to send a duplicate of an old SeqNo={request.SeqNo}");

            return new RpcResult()
            {
                Result = call.CreateResult(response),
            };
        }

        if (currentTransactions.Any(c =>
                c.EventType != nameof(TransactionEventEnum.Updated) && c.EventType == Enum.GetName(request.EventType)))
        {
            _logger.LogWarning(
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

    private TransactionConsumption? GetTransactionConsumption(TransactionEventRequest request)
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