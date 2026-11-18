using Google.Protobuf.WellKnownTypes;
using Medinilla.Core.SharedContracts.Events;
using Medinilla.DataTypes.Contracts;

namespace Medinilla.Core.Events;

/// <summary>
/// Builds <see cref="BootupEvent"/> from a successfully processed, accepted BootNotification.
/// Pure transformation: no database, clock, or other I/O.
/// </summary>
public sealed class BootupEventMapper
{
    public BootupEvent Map(Guid accountId, string chargerId, BootNotificationRequest request)
    {
        var @event = new BootupEvent
        {
            ChargerId = chargerId,
            AccountId = accountId.ToString("D"),
            BootReason = request.Reason.ToString(),
            VendorName = request.ChargingStation.VendorName ?? string.Empty,
            Model = request.ChargingStation.Model ?? string.Empty,
        };

        if (!string.IsNullOrWhiteSpace(request.ChargingStation.SerialNumber))
        {
            @event.SerialNumber = request.ChargingStation.SerialNumber;
        }

        if (!string.IsNullOrWhiteSpace(request.ChargingStation.FirmwareVersion))
        {
            @event.FirmwareVersion = request.ChargingStation.FirmwareVersion;
        }

        return @event;
    }
}

/// <summary>
/// Builds <see cref="StatusNotificationEvent"/> from a committed StatusNotification.
/// Status events carry identity, status, and the reported timestamp only; connector
/// type belongs to registration inventory.
/// </summary>
public sealed class StatusNotificationEventMapper
{
    public StatusNotificationEvent Map(StatusNotificationSnapshot snapshot)
    {
        return new StatusNotificationEvent
        {
            ChargerId = snapshot.ChargerId,
            AccountId = snapshot.AccountId.ToString("D"),
            EvseId = snapshot.EvseId,
            ConnectorId = snapshot.ConnectorId,
            ConnectorStatus = snapshot.ConnectorStatus,
            StatusTimestamp = Timestamp.FromDateTime(snapshot.StatusTimestampUtc),
        };
    }
}

/// <summary>
/// Builds the shared <see cref="TransactionLifecycleEvent"/> from a materialized
/// transaction completion snapshot. Pure transformation only.
/// </summary>
public sealed class TransactionLifecycleEventMapper
{
    public TransactionLifecycleEvent Map(TransactionLifecycleSnapshot snapshot)
    {
        var @event = new TransactionLifecycleEvent
        {
            ChargerId = snapshot.ChargerId,
            AccountId = snapshot.AccountId.ToString("D"),
            TransactionId = snapshot.TransactionId,
        };

        if (snapshot.ConsumptionWh.HasValue)
        {
            @event.ConsumptionWh = (double)snapshot.ConsumptionWh.Value;
        }

        return @event;
    }
}

/// <summary>
/// Builds <see cref="ChargerRegistrationCompletedEvent"/> from the committed
/// registration inventory snapshot. Pure transformation only.
/// </summary>
public sealed class ChargerRegistrationCompletedEventMapper
{
    public ChargerRegistrationCompletedEvent Map(ChargerRegistrationSnapshot snapshot)
    {
        var @event = new ChargerRegistrationCompletedEvent
        {
            ChargerId = snapshot.ChargerId,
            AccountId = snapshot.AccountId.ToString("D"),
        };

        foreach (var connector in snapshot.Connectors
                     .OrderBy(connector => connector.EvseId)
                     .ThenBy(connector => connector.ConnectorId))
        {
            var registeredConnector = new RegisteredConnector
            {
                EvseId = connector.EvseId,
                ConnectorId = connector.ConnectorId,
            };

            if (!string.IsNullOrWhiteSpace(connector.ConnectorType))
            {
                registeredConnector.ConnectorType = connector.ConnectorType;
            }

            @event.Connectors.Add(registeredConnector);
        }

        return @event;
    }
}
