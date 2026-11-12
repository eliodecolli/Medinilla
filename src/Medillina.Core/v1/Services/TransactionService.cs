using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.Logic.Authorization;
using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.Enums;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using OcppIdToken = Medinilla.DataTypes.Contracts.Common.IdToken;
using Microsoft.Extensions.Logging;
using ChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;
using IdToken = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;

namespace Medinilla.Core.v1.Services;

public class TransactionService(
    ILogger<ITransactionService> logger,
    MedinillaOcppDbContext context,
    AuthorizationAlgorithmFactory authFactory)
    : ITransactionService
{
    private TransactionEvent MapEvent(TransactionEventRequest request)
    {
        return new TransactionEvent
        {
            TransactionId = request.TransactionInfo.TransactionId,
            EventType = Enum.Parse<TransactionEventType>(request.EventType.ToString()),
            Timestamp = request.Timestamp.ToUniversalTime(),
            TriggerReason = Enum.Parse<TransactionTriggerReason>(request.TriggerReason.ToString()),
            SeqNo = request.SeqNo,
            Offline = request.Offline,
            NumberOfPhasesUsed = request.NumberOfPhasesUsed,
            CableMaxCurrent = request.CableMaxCurrent,
            ReservationId = request.ReservationId,
            ChargingState = request.TransactionInfo.ChargingState.HasValue
                ? Enum.Parse<ChargingState>(request.TransactionInfo.ChargingState.Value.ToString())
                : null,
            TimeSpentCharging = request.TransactionInfo.TimeSpentCharging,
            StoppedReason = request.TransactionInfo.StoppedReason.HasValue
                ? Enum.Parse<TransactionStoppedReason>(request.TransactionInfo.StoppedReason.Value.ToString())
                : null,
            RemoteStartId = request.TransactionInfo.RemoteStartId,
            EvseId = request.Evse?.Id,
            ConnectorId = request.Evse?.ConnectorId,
        };
    }
    
    private decimal NormalizeSampleValue(SampledValue sample)
    {
        var multiplier = sample.UnitOfMeasure?.Multiplier ?? 0;
        var unitFactor = sample.UnitOfMeasure?.Unit == "kWh" ? 1000m : 1m;
        return sample.Value * (decimal)Math.Pow(10, multiplier) * unitFactor;
    }

    private ICollection<TransactionSample> MapSamples(
        IEnumerable<MeterValue>? meterValues, TransactionEvent transactionEvent)
    {
        return meterValues?.SelectMany(meter => meter.SampledValue.Select(sample => new TransactionSample
        {
            Timestamp = meter.Timestamp.ToUniversalTime(),
            Measurand = Enum.Parse<Measurand>(
                (sample.Measurand ?? MeasurandEnum.EnergyActiveImportRegister).ToString()),
            Location = Enum.Parse<MeasurementLocation>(
                (sample.Location ?? LocationEnum.Outlet).ToString()),
            Value = NormalizeSampleValue(sample),
        })).ToList() ?? [];
    }

    private bool IsEventPresent(ChargingStation cs, string transactionId, int seqNo)
    {
        return cs.TransactionEvents.Any(x => x.TransactionId == transactionId && x.SeqNo == seqNo);
    }
    
    public async Task<TransactionEventResponse> IngestEvent(string clientIdentifier, TransactionEventRequest request)
    {
        TransactionEventResponse result = new();
        ChargingStation chargingStation;
        
        try
        {
            chargingStation = await context.GetChargingStation(clientIdentifier);
        }
        catch (AggregateRootNotFoundException e)
        {
            logger.LogError("Transaction Event Error: Could not find charging station with id {ci}", clientIdentifier);
            throw; // just let the caller take care of this
        }

        // first validate for double events
        if (IsEventPresent(chargingStation, request.TransactionInfo.TransactionId, request.SeqNo))
        {
            logger.LogInformation(
                "{ci}: Transaction Event: SeqNo {sn} for {tx} already processed.",
                clientIdentifier,
                request.SeqNo,
                request.TransactionInfo.TransactionId);
            return result;
        }
        
        var @event = MapEvent(request);
        
        // if we have a token id present then compute it - if not then try to associate one with the updated event
        // if we DO have a token and it can't be authorized then fuck it
        IdToken? token;
        var isTokenPresent = false;
        
        if (request.IdToken is not null)
        {
            token = chargingStation.IdTokens.FirstOrDefault(x => x.Token == request.IdToken.Token);
            isTokenPresent = true;
        }
        else
        {
            token = chargingStation.TransactionEvents
                .FirstOrDefault(tx =>
                    tx.TransactionId == request.TransactionInfo.TransactionId &&
                    tx.IdToken is not null)?.IdToken;
        }

        var authContext = AuthUtils.GenerateAuthContext(chargingStation, request.Evse?.Id, token);
        var authResult = await authFactory.RunAuthorization(authContext);
        if (authResult != AuthorizeStatus.Accepted)
        {
            // yeesh - let them know
            logger.LogError(
                "{ci}: Transaction Event: Authorization failed for tx={tx} status={authResult}",
                clientIdentifier,
                request.TransactionInfo.TransactionId,
                authResult);
            
            result.IdTokenInfo = new IdTokenInfo()
            {
                Status = authResult,
            };
            return result;
        }

        if (token is null)
        {
            logger.LogError(
                "{ci}: Transaction Event: IdToken for this transaction could not be found tx={tx}",
                clientIdentifier,
                request.TransactionInfo.TransactionId);
        }
        else
        {
            // if the token is present update the response to include it
            if (isTokenPresent)
            {
                result.IdTokenInfo = new IdTokenInfo()
                {
                    Status = authResult,
                };
            }
            
            @event.IdToken = token;
        }
        
        // fill in the samples
        @event.Samples = MapSamples(request.MeterValue, @event);
        
        // finally append the event
        chargingStation.TransactionEvents.Add(@event);
        
        await context.SaveChangesAsync();
        
        return result;
    }
}