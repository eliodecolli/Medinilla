using System.Collections.Immutable;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.Logic.Authorization;
using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.Enums;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Microsoft.Extensions.Logging;
using ChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;
using IdToken = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;
using IdTokenType = Medinilla.DataAccess.Relational.Enums.IdTokenType;

namespace Medinilla.Core.v1.Services;

public class TransactionService(
    ILogger<ITransactionService> logger,
    MedinillaOcppDbContext context,
    AuthorizationAlgorithmFactory authFactory,
    IIdTokenService tokenService,
    IConsumptionService consumptionService)
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
    
    private HashSet<Measurand>? ParseAllowedMeasurands(AccountSettings? settings)
    {
        var allowed = new HashSet<Measurand>();

        if (settings is null || string.IsNullOrWhiteSpace(settings.AllowedMeasurands))
        {
            logger.LogWarning(
                "No allowed measurands are configured for this account, all sampled value will be stored.");
            return null;
        }

        foreach (var entry in settings.AllowedMeasurands.Split(
                     ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<Measurand>(entry, true, out var measurand))
            {
                allowed.Add(measurand);
            }
            else
            {
                logger.LogWarning("Ignoring unknown measurand {measurand} in the allowed measurands settings", entry);
            }
        }

        return allowed;
    }

    private ICollection<TransactionSample> MapSamples(
        ChargingStation chargingStation,
        string transactionId,
        IEnumerable<MeterValue>? meterValues,
        AccountSettings? settings)
    {
        var allowedMeasurands = ParseAllowedMeasurands(settings);

        return meterValues?.SelectMany(meter => meter.SampledValue.Select(sample => new
        {
            Meter = meter,
            Sample = sample,
            Measurand = Enum.Parse<Measurand>(
                (sample.Measurand ?? MeasurandEnum.EnergyActiveImportRegister).ToString()),
        }))
            .Where(x => allowedMeasurands?.Contains(x.Measurand) ?? true )
            .Select(x => new TransactionSample
            {
                TransactionId = transactionId,
                Timestamp = x.Meter.Timestamp.ToUniversalTime(),
                Measurand = x.Measurand,
                Location = Enum.Parse<MeasurementLocation>(
                    (x.Sample.Location ?? LocationEnum.Outlet).ToString()),
                Context = Enum.Parse<ReadingContext>(
                    (x.Sample.Context ?? ReadingContextEnum.SamplePeriodic).ToString()),
                Phase = x.Sample.Phase.HasValue
                    ? Enum.Parse<MeasurementPhase>(x.Sample.Phase.Value.ToString())
                    : null,
                Value = NormalizeSampleValue(x.Sample),
                ChargingStation = chargingStation
            }).ToList() ?? [];
    }

    private bool IsEventPresent(ChargingStation cs, string transactionId, int seqNo)
    {
        return cs.TransactionEvents.Any(x => x.TransactionId == transactionId && x.SeqNo == seqNo);
    }

    private bool IsTransactionComplete(ChargingStation cs, string transactionId)
    {
        var txes = cs.TransactionEvents
            .Where(tx => tx.TransactionId == transactionId).ToList();

        var seqNoSet = txes.Select(tx => tx.SeqNo).ToImmutableSortedSet();
        var isContinuous = seqNoSet.Count > 0 && (seqNoSet.Max - seqNoSet.Min + 1) == seqNoSet.Count;
        return txes.Any(tx => tx.EventType == TransactionEventType.Started) &&
               txes.Any(tx => tx.EventType == TransactionEventType.Ended) &&
               isContinuous;
    }
    
    public async Task<TransactionIngestionResult> IngestEvent(string clientIdentifier, TransactionEventRequest request)
    {
        TransactionEventResponse result = new();
        ChargingStation chargingStation;
        AccountSettings? settings;
        
        try
        {
            chargingStation = await context.GetChargingStation(clientIdentifier);
            settings = chargingStation.Account?.Settings;
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
            return new TransactionIngestionResult(result, WasNewlyIngested: false);
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
            return new TransactionIngestionResult(result, WasNewlyIngested: false);
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
        @event.Samples = MapSamples(chargingStation, request.TransactionInfo.TransactionId, request.MeterValue, settings);
        
        // finally append the event
        chargingStation.TransactionEvents.Add(@event);
        
        // first check whether this is an Ended event, if it is, create a snapshot
        if (@event.EventType == TransactionEventType.Ended)
        {
            // Track the incoming samples before reading the station's inverse collection.
            context.ChangeTracker.DetectChanges();
            var samples = chargingStation.TransactionSamples
                .Where(s => s.TransactionId == request.TransactionInfo.TransactionId);

            var snapshot = new TransactionSnapshot()
            {
                TransactionId = request.TransactionInfo.TransactionId,
                Consumption = await consumptionService.TryComputeConsumption(samples)
            };
            chargingStation.TransactionSnapshots.Add(snapshot);
            
            logger.LogInformation(
                "{ci}: Transaction Event: Computed final consumption {cp} Wh for tx={tx}",
                clientIdentifier,
                snapshot.Consumption,
                request.TransactionInfo.TransactionId);
        }
        
        
        // if this is the end then we should free the token now
        // one caveat: there's nothing guaranteeing us that the Ended event will come last
        // thus the check should be: ALL SEQUENCE NUMBERS PRESENT + "Ended" EVENT PRESENT + TOKEN IS TYPE "Central"
        if (IsTransactionComplete(chargingStation, request.TransactionInfo.TransactionId) 
            && token is not null
            && token.IdType == IdTokenType.Central)
        {
            await tokenService.ReleaseCentralToken(token);
        }
        
        await context.SaveChangesAsync();
        
        return new TransactionIngestionResult(result, WasNewlyIngested: true);
    }
}