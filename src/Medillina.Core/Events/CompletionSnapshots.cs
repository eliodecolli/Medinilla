namespace Medinilla.Core.Events;

/// <summary>
/// Fully materialized connector status values prepared after a committed
/// StatusNotification. The timestamp is the effective value persisted by the workflow.
/// </summary>
public sealed record StatusNotificationSnapshot(
    string ChargerId,
    int EvseId,
    int ConnectorId,
    string ConnectorStatus,
    DateTime StatusTimestampUtc);

/// <summary>
/// Fully materialized transaction lifecycle values prepared by the transaction
/// workflow. No database or deferred-query access is allowed from mapping.
/// </summary>
public sealed record TransactionLifecycleSnapshot(
    string ChargerId,
    string TransactionId,
    decimal? ConsumptionWh);

/// <summary>
/// Fully materialized registration inventory snapshot prepared from the charger's stored
/// connector components. The report-specific versus accumulated scope remains unresolved.
/// </summary>
public sealed record ChargerRegistrationSnapshot(
    string ChargerId,
    IReadOnlyList<RegisteredConnectorSnapshot> Connectors);

public sealed record RegisteredConnectorSnapshot(
    int EvseId,
    int ConnectorId,
    string? ConnectorType);
