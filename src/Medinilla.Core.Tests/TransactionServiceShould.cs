using Medinilla.Core.Logic.Exceptions;
using Medinilla.Core.v1.Services;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Medinilla.DataAccess.Relational.UnitOfWork;
using ChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;
using IdToken = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;

namespace Medinilla.Core.Tests;

/// <summary>
/// Covers the new behaviour introduced in <c>feat/transaction-overhaul</c>:
/// <list type="bullet">
///   <item><see cref="TransactionService.FinalizeTransaction"/> guards against duplicate
///         snapshots for the same OCPP transaction (concurrent Ended events race).</item>
///   <item><see cref="TransactionService.GetTransactionUnit"/> resolves the unit name
///         persisted on the most recent <see cref="TransactionEvent"/> for a transaction.</item>
/// </list>
/// </summary>
public class TransactionServiceShould
{
    private const string TransactionId = "tx-1";
    private const string ClientId = "CS-001";
    private const string UnitName = "Wh";

    // The unit of work is unused by FinalizeTransaction and GetTransactionUnit; we pass null
    // to avoid spinning up an EF InMemory context here (kept lean — the action layer is what
    // tests the full plumbing).
    private static TransactionService CreateSut() => new(null!);

    private static ChargingStation NewChargingStation() => new()
    {
        Id = Guid.NewGuid(),
        ClientIdentifier = ClientId,
        Vendor = "TestVendor",
        Model = "TestModel",
    };

    private static TransactionSnapshot NewSnapshot(string transactionId) => new()
    {
        Id = Guid.NewGuid(),
        TransactionId = transactionId,
        ChargingStationId = Guid.NewGuid(),
        TotalCost = 1.23m,
        StartedAt = DateTime.UtcNow.AddMinutes(-10),
        EndedAt = DateTime.UtcNow,
        StartReason = "Authorized",
        EndReason = "EVDisconnected",
    };

    [Fact]
    public void FinalizeTransaction_AddsSnapshot_WhenNoExistingSnapshot()
    {
        // Arrange
        var sut = CreateSut();
        var chargingStation = NewChargingStation();
        var snapshot = NewSnapshot(TransactionId);

        // Act
        sut.FinalizeTransaction(chargingStation, snapshot);

        // Assert: the snapshot was added and is the only one present.
        Assert.Single(chargingStation.TransactionSnapshots);
        Assert.Same(snapshot, chargingStation.TransactionSnapshots.Single());
    }

    [Fact]
    public void FinalizeTransaction_ThrowsTransactionException_WhenSnapshotAlreadyExistsForSameTransactionId()
    {
        // Arrange: a snapshot for the same TransactionId has already been persisted (e.g.
        // a previous Ended event won the race). The second call must surface as
        // TransactionException so the action layer can swallow + log it.
        var sut = CreateSut();
        var chargingStation = NewChargingStation();
        var first = NewSnapshot(TransactionId);
        chargingStation.TransactionSnapshots.Add(first);

        var second = NewSnapshot(TransactionId);

        // Act + Assert
        var ex = Assert.Throws<TransactionException>(
            () => sut.FinalizeTransaction(chargingStation, second));
        Assert.Contains(TransactionId, ex.Message);

        // The existing snapshot must NOT be replaced, and the second one must not be appended.
        Assert.Single(chargingStation.TransactionSnapshots);
        Assert.Same(first, chargingStation.TransactionSnapshots.Single());
    }

    [Fact]
    public void FinalizeTransaction_DoesNotThrow_WhenExistingSnapshotBelongsToDifferentTransactionId()
    {
        // Arrange: existing snapshot for a different OCPP transaction — this is fine.
        var sut = CreateSut();
        var chargingStation = NewChargingStation();
        chargingStation.TransactionSnapshots.Add(NewSnapshot("some-other-tx"));

        var newSnapshot = NewSnapshot(TransactionId);

        // Act + Assert: must not throw.
        sut.FinalizeTransaction(chargingStation, newSnapshot);

        Assert.Equal(2, chargingStation.TransactionSnapshots.Count);
        Assert.Contains(newSnapshot, chargingStation.TransactionSnapshots);
    }

    // -----------------------------------------------------------------------------------------
    // GetTransactionUnit
    // -----------------------------------------------------------------------------------------
    // The action layer forwards this unit name into tariffService.CalculateTotalCosts
    // (see TransactionEventAction's Ended branch), so the lookup must return the unit
    // recorded on the transaction's events — or null when nothing useful was reported.

    [Fact]
    public async Task GetTransactionUnit_ReturnsUnitName_FromMatchingTransactionEvent()
    {
        var sut = CreateSut();
        var chargingStation = NewChargingStation();
        chargingStation.TransactionEvents.Add(NewTransactionEvent(TransactionId, UnitName));

        var unit = await sut.GetTransactionUnit(chargingStation, TransactionId);

        Assert.Equal(UnitName, unit);
    }

    [Fact]
    public async Task GetTransactionUnit_ReturnsNull_WhenNoEventMatchesTransactionId()
    {
        var sut = CreateSut();
        var chargingStation = NewChargingStation();
        chargingStation.TransactionEvents.Add(NewTransactionEvent("some-other-tx", UnitName));

        var unit = await sut.GetTransactionUnit(chargingStation, TransactionId);

        Assert.Null(unit);
    }

    [Fact]
    public async Task GetTransactionUnit_ReturnsNull_WhenMatchingEventHasEmptyUnitName()
    {
        // A charger may persist events with no unit name (MapOcppTransaction falls back to
        // "UNKNOWN" when no meter value carries a UnitOfMeasure, but that string could also
        // be replaced with empty in other code paths). Either way, we should refuse to return
        // empty strings so the action layer can apply its "?? \"UNKNOWN\"" fallback.
        var sut = CreateSut();
        var chargingStation = NewChargingStation();
        chargingStation.TransactionEvents.Add(NewTransactionEvent(TransactionId, unitName: ""));

        var unit = await sut.GetTransactionUnit(chargingStation, TransactionId);

        Assert.Null(unit);
    }

    // -----------------------------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------------------------

    private static TransactionEvent NewTransactionEvent(string transactionId, string unitName) => new()
    {
        Id = Guid.NewGuid(),
        TransactionId = transactionId,
        SeqNo = 0,
        UnitName = unitName,
        EventType = "Started",
        TriggerReason = "Authorized",
        Timestamp = DateTime.UtcNow,
        IdToken = new IdToken { Id = Guid.NewGuid() },
    };
}