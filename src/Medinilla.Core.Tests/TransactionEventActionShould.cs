using Medinilla.Core.Actions.Ocpp201;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.Logic.Authorization;
using Medinilla.Core.v1.Transactions;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Enums;
using Medinilla.Infrastructure;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;
using ChargingStationDb = Medinilla.DataAccess.Relational.Models.ChargingStation;
using IdTokenDb = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;
using ContractIdToken = Medinilla.DataTypes.Contracts.Common.IdToken;

namespace Medinilla.Core.Tests;

public class TransactionEventActionShould
{
    private readonly ITestOutputHelper _testOutputHelper;

    public TransactionEventActionShould(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }
    
    private const float EXPECTED_TOTAL_CONSUMPTION_WH_1 = 51f; // Total: 51 Wh
    private const float EXPECTED_TOTAL_CONSUMPTION_WH_2 = 41f; // Ditto

    private async Task<TransactionEventRequest> GetRequest(string fileName)
    {
        var jsonPath = Path.Combine("Data", fileName);
        var jsonContent = await File.ReadAllTextAsync(jsonPath);
        var jsonArray = JsonDocument.Parse(jsonContent).RootElement;

        var payloadJson = jsonArray[3].GetRawText();
        var request = JsonSerializer.Deserialize<TransactionEventRequest>(payloadJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new DottedEnumJsonConverter() }
        });
        
        return request;
    }

    private async Task AssertCalculation(string fileName, float expected)
    {
        var request = await GetRequest(fileName);
        Assert.NotNull(request);
        Assert.NotNull(request.MeterValue);

        // Create the TransactionService (this is what actually calculates consumption)
        var transactionService = new ConsumptionService();

        // Act - Calculate consumption from meter values
        // The JSON has meter values at Transaction.Begin and Transaction.End
        // The service processes these to determine the consumption
        var consumption = transactionService.GetTransactionConsumption(request.MeterValue);

        // Assert
        Assert.NotNull(consumption);

        // CURRENT BEHAVIOR: The implementation returns the LAST Energy.Active.Import.Register value found
        // In the JSON, the order is: total (51 Wh), L1 (17 Wh), L2 (17 Wh), L3 (17 Wh)
        // The service iterates and the last one (L3) becomes the result: 0.017 kWh
        Assert.Equal(expected, consumption.Consumption);

        // EXPECTED BEHAVIOR: Should use the total value without phase (51 Wh)
        // Assert.Equal(EXPECTED_TOTAL_CONSUMPTION_KWH, consumption.Consumption); // Uncomment when fixed

        Assert.Equal(Medinilla.DataTypes.Core.Enums.ConsumptionType.Cumulative, consumption.ConsumptionType);
    }

    [Fact]
    public async Task CalculateCorrectConsumptionFromTransactionEventEndJson()
    {
        await AssertCalculation("TransactionEventEnd.json", EXPECTED_TOTAL_CONSUMPTION_WH_1);
        await AssertCalculation("TransactionEventEnd2.json", EXPECTED_TOTAL_CONSUMPTION_WH_2);
    }

    [Fact]
    public async Task MergesConsumptionGraphs()
    {
        var txService = new ConsumptionService();
        
        var request1 = await GetRequest("TransactionEventEnd.json");

        var graph1 = txService.GetConsumptionGraph(request1.MeterValue);
        Assert.NotNull(graph1);

        var request2 = await GetRequest("TransactionEventEnd2.json");
        
        var graph2 =  txService.GetConsumptionGraph(request2.MeterValue);
        Assert.NotNull(graph2);
        
        Assert.Equal(EXPECTED_TOTAL_CONSUMPTION_WH_1, txService.GetTransactionConsumption(graph1).Consumption);
        Assert.Equal(EXPECTED_TOTAL_CONSUMPTION_WH_2, txService.GetTransactionConsumption(graph2).Consumption);
        
        var expected = EXPECTED_TOTAL_CONSUMPTION_WH_1 + EXPECTED_TOTAL_CONSUMPTION_WH_2;
        Assert.Equal(expected, txService.GetTransactionConsumption(request2.MeterValue, graph1).Consumption);
    }

    [Fact]
    public async Task MergesConsumptionGraphsByOperator()
    {
        var txService =  new ConsumptionService();
        
        var request1 = await GetRequest("TransactionEventEnd.json");

        var graph1 = txService.GetConsumptionGraph(request1.MeterValue);
        Assert.NotNull(graph1);

        var request2 = await GetRequest("TransactionEventEnd2.json");
        
        var graph2 =  txService.GetConsumptionGraph(request2.MeterValue);
        Assert.NotNull(graph2);
        
        Assert.Equal(EXPECTED_TOTAL_CONSUMPTION_WH_1, txService.GetTransactionConsumption(graph1).Consumption);
        Assert.Equal(EXPECTED_TOTAL_CONSUMPTION_WH_2, txService.GetTransactionConsumption(graph2).Consumption);
        
        var expected = EXPECTED_TOTAL_CONSUMPTION_WH_1 + EXPECTED_TOTAL_CONSUMPTION_WH_2;
        
        var finalGraph = graph1 << graph2;
        Assert.NotNull(finalGraph);
        Assert.Equal(expected, txService.GetTransactionConsumption(finalGraph).Consumption);

        var ff = finalGraph << graph2 << graph1;
        Assert.NotNull(ff);
        Assert.Equal(expected * 2, txService.GetTransactionConsumption(ff).Consumption);
    }

    // =====================================================================================================
    // TransactionEventAction (Ended-flow) tests
    //
    // These exercise the new behaviour introduced in feat/transaction-overhaul:
    //   - response.TotalCost is computed via tariffService.CalculateTotalCosts (not raw consumption)
    //   - the TransactionSnapshot's TotalCost matches the tariffed cost
    //   - the unit name returned by ITransactionService.GetTransactionUnit is forwarded into the tariff
    //     calc, with a "UNKNOWN" fallback when null
    //   - the snapshot is persisted via ITransactionService.FinalizeTransaction (race-safe)
    //   - a TransactionException thrown by FinalizeTransaction is caught + logged, not propagated
    //   - no snapshot / finalize is attempted when there are no prior transaction events
    // =====================================================================================================

    private const string ClientId = "CS-TEST-001";
    private const string TxId = "tx-001";
    private const string Token = "TOKEN-001";
    private const decimal ConsumptionWh = 51m;
    private const decimal UnitPrice = 0.42m;

    // -- Response TotalCost is computed via tariffService ---------------------------

    [Fact]
    public async Task EndedEvent_SetsResponseTotalCost_FromTariffService()
    {
        var (sut, ctx) = BuildEndedScenario(unitName: "Wh");
        var expectedCost = (float)ConsumptionWh * (float)UnitPrice; // 21.42

        var result = await sut.Execute(ctx.Call, ClientId);

        Assert.NotNull(result.Result);
        var response = result.Result!.PayloadAs<TransactionEventResponse>();
        Assert.Equal((decimal)expectedCost, response.TotalCost);
    }

    // -- Snapshot TotalCost is the tariffed cost (not raw consumption) ----------------

    [Fact]
    public async Task EndedEvent_BuildsSnapshot_WithComputedTotalCost()
    {
        // Regression guard: prior to the refactor, snapshot.TotalCost was the raw consumption
        // value (Convert.ToDecimal(consumption.Consumption)). It must now match the tariffed cost.
        var (sut, ctx) = BuildEndedScenario(unitName: "Wh");
        var expectedCost = (float)ConsumptionWh * (float)UnitPrice;

        var result = await sut.Execute(ctx.Call, ClientId);

        ctx.TransactionServiceMock.Verify(
            s => s.FinalizeTransaction(
                It.IsAny<ChargingStationDb>(),
                It.Is<TransactionSnapshot>(s => s.TotalCost == (decimal)expectedCost)),
            Times.Once);
        // And the raw consumption must NOT leak into the snapshot.
        ctx.TransactionServiceMock.Verify(
            s => s.FinalizeTransaction(
                It.IsAny<ChargingStationDb>(),
                It.Is<TransactionSnapshot>(s => s.TotalCost == ConsumptionWh)),
            Times.Never);
    }

    // -- Unit name from GetTransactionUnit is forwarded to tariffService --------------

    [Fact]
    public async Task EndedEvent_PassesUnitNameFromGetTransactionUnit_ToTariffService()
    {
        var (sut, ctx) = BuildEndedScenario(unitName: "kWh");

        await sut.Execute(ctx.Call, ClientId);

        ctx.TariffServiceMock.Verify(
            t => t.CalculateTotalCosts(It.IsAny<float>(), It.IsAny<ChargingStationDb>(), "kWh"),
            Times.Once);
    }

    [Fact]
    public async Task EndedEvent_FallsBackToUnknownUnit_WhenGetTransactionUnitReturnsNull()
    {
        var (sut, ctx) = BuildEndedScenario(unitName: null);

        await sut.Execute(ctx.Call, ClientId);

        ctx.TariffServiceMock.Verify(
            t => t.CalculateTotalCosts(It.IsAny<float>(), It.IsAny<ChargingStationDb>(), "UNKNOWN"),
            Times.Once);
    }

    // -- FinalizeTransaction called once on the happy path -----------------------------

    [Fact]
    public async Task EndedEvent_CallsFinalizeTransaction_Once_OnHappyPath()
    {
        var (sut, ctx) = BuildEndedScenario(unitName: "Wh");

        await sut.Execute(ctx.Call, ClientId);

        ctx.TransactionServiceMock.Verify(
            s => s.FinalizeTransaction(It.IsAny<ChargingStationDb>(), It.IsAny<TransactionSnapshot>()),
            Times.Once);
    }

    // -- TransactionException from FinalizeTransaction is caught (race handling) -------

    [Fact]
    public async Task EndedEvent_DoesNotPropagateTransactionException_OnFinalizeRace()
    {
        // Race condition: another request has already finalized this transaction. The service
        // throws TransactionException; the action layer must catch it, log, and return a normal
        // response (rather than failing the OCPP call with an error).
        var (sut, ctx) = BuildEndedScenario(unitName: "Wh");
        ctx.TransactionServiceMock
            .Setup(s => s.FinalizeTransaction(It.IsAny<ChargingStationDb>(), It.IsAny<TransactionSnapshot>()))
            .Throws(new Medinilla.Core.Logic.Exceptions.TransactionException("race"));

        var result = await sut.Execute(ctx.Call, ClientId);

        Assert.NotNull(result.Result);
        Assert.Null(result.Error);
        // The charger still gets a valid TotalCost computed from the tariff.
        var response = result.Result!.PayloadAs<TransactionEventResponse>();
        Assert.NotNull(response.TotalCost);
    }

    // -- No prior events: snapshot is not built, FinalizeTransaction is not called ----

    [Fact]
    public async Task EndedEvent_DoesNotCallFinalizeTransaction_WhenNoPriorEvents()
    {
        // When the charger sends Ended without any prior Started/Updated events for this
        // transactionId (currentTransactions is empty), we skip snapshot creation entirely.
        // (This is an unusual but valid case — e.g. a charging station that lost state.)
        var (sut, ctx) = BuildEndedScenario(unitName: "Wh", includePriorEvents: false);

        await sut.Execute(ctx.Call, ClientId);

        ctx.TransactionServiceMock.Verify(
            s => s.FinalizeTransaction(It.IsAny<ChargingStationDb>(), It.IsAny<TransactionSnapshot>()),
            Times.Never);
    }

    // --------------------------------------------------------------------------------------------------------
    // Ended-scenario harness
    // --------------------------------------------------------------------------------------------------------

    private sealed record Scenario(
        Mock<IChargerService> ChargerServiceMock,
        Mock<ITransactionService> TransactionServiceMock,
        Mock<ITariffService> TariffServiceMock,
        OcppCallRequest Call);

    /// <summary>
    /// Builds a fully-wired <see cref="TransactionEventAction"/> with mocks and a charging station
    /// primed for the <c>Ended</c>-branch path of the diff under test.
    /// </summary>
    /// <param name="unitName">Value returned by <c>GetTransactionUnit</c>. <c>null</c> exercises the "UNKNOWN" fallback.</param>
    /// <param name="includePriorEvents">If <c>false</c>, the charging station has no events for <see cref="TxId"/>.</param>
    private (TransactionEventAction Sut, Scenario Ctx) BuildEndedScenario(string? unitName, bool includePriorEvents = true)
    {
        // ---- Charging station + prior events -------------------------------------------------
        var idToken = new IdTokenDb
        {
            Id = Guid.NewGuid(),
            ChargingStationId = Guid.NewGuid(),
            Token = Token,
            IdType = nameof(IdTokenType.Local),
            AuthorizationUserId = Guid.NewGuid(),
        };
        var chargingStation = new ChargingStationDb
        {
            Id = Guid.NewGuid(),
            ClientIdentifier = ClientId,
            Vendor = "TestVendor",
            Model = "TestModel",
            IdTokens = new List<IdTokenDb> { idToken },
            TransactionEvents = includePriorEvents
                ? new List<TransactionEvent>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        TransactionId = TxId,
                        SeqNo = 0,
                        EventType = nameof(TransactionEventEnum.Started),
                        TriggerReason = nameof(TriggerReasonEnum.Authorized),
                        Timestamp = DateTime.UtcNow.AddMinutes(-10),
                        UnitName = "Wh",
                        ChargingStationId = Guid.NewGuid(),
                        IdTokenId = idToken.Id,
                        IdToken = idToken,
                    },
                }
                : new List<TransactionEvent>(),
        };

        // ---- Ended-event request payload ------------------------------------------------------
        var payload = new TransactionEventRequest
        {
            EventType = TransactionEventEnum.Ended,
            Timestamp = DateTime.UtcNow,
            SeqNo = 1, // strictly greater than the Started event's SeqNo (0)
            TriggerReason = TriggerReasonEnum.EVCommunicationLost,
            TransactionInfo = new Transaction { TransactionId = TxId },
            IdToken = new ContractIdToken { Token = Token, Type = IdTokenType.Local },
            MeterValue = new List<MeterValue>
            {
                new()
                {
                    Timestamp = DateTime.UtcNow.AddMinutes(-10),
                    SampledValue = new List<SampledValue>
                    {
                        new()
                        {
                            Value = 0m, // Tx.Begin reading
                            Context = ReadingContextEnum.TransactionBegin,
                            Measurand = MeasurandEnum.EnergyActiveImportRegister,
                            Location = LocationEnum.Outlet,
                            UnitOfMeasure = new UnitOfMeasure { Unit = "Wh" },
                        },
                    },
                },
                new()
                {
                    Timestamp = DateTime.UtcNow,
                    SampledValue = new List<SampledValue>
                    {
                        new()
                        {
                            Value = ConsumptionWh, // Tx.End reading
                            Context = ReadingContextEnum.TransactionEnd,
                            Measurand = MeasurandEnum.EnergyActiveImportRegister,
                            Location = LocationEnum.Outlet,
                            UnitOfMeasure = new UnitOfMeasure { Unit = "Wh" },
                        },
                    },
                },
            },
        };
        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        });
        var call = new OcppCallRequest("msg-1", OcppActionNames.TransactionEvent, payloadJson);

        // ---- Mocks ----------------------------------------------------------------------------
        var chargerServiceMock = new Mock<IChargerService>();
        chargerServiceMock
            .Setup(s => s.GetByClientIdentifier(ClientId))
            .ReturnsAsync(chargingStation);

        var transactionServiceMock = new Mock<ITransactionService>();
        transactionServiceMock
            .Setup(s => s.GetTransactionUnit(It.IsAny<ChargingStationDb>(), TxId))
            .ReturnsAsync(unitName);

        var tariffServiceMock = new Mock<ITariffService>();
        tariffServiceMock
            .Setup(t => t.CalculateTotalCosts(It.IsAny<float>(), It.IsAny<ChargingStationDb>(), It.IsAny<string>()))
            .Returns<float, ChargingStationDb, string>((consumption, _, _) => (decimal)consumption * UnitPrice);

        var idTokenServiceMock = new Mock<IIdTokenService>();
        idTokenServiceMock
            .Setup(s => s.TryGetForTransaction(It.IsAny<ChargingStationDb>(), It.IsAny<IEnumerable<TransactionEvent>>(), Token))
            .Returns(idToken);
        idTokenServiceMock
            .Setup(s => s.RemoveTemporaryToken(It.IsAny<ChargingStationDb>(), It.IsAny<IdTokenDb>()))
            .Returns(false);

        var unitOfWorkWrapperMock = new Mock<IUnitOfWorkWrapper>();
        unitOfWorkWrapperMock
            .Setup(u => u.SaveChanges())
            .Returns(Task.CompletedTask);

        // AuthorizationAlgorithmFactory is sealed and not mockable; pass an empty algorithm set
        // so RunAuthorization returns Accepted without running any checks.
        var authFactory = new AuthorizationAlgorithmFactory(
            Array.Empty<Medinilla.Core.Interfaces.IAuthAlgorithm>(),
            NullLogger<AuthorizationAlgorithmFactory>.Instance);

        var sut = new TransactionEventAction(
            NullLogger<TransactionEventAction>.Instance,
            chargerServiceMock.Object,
            unitOfWorkWrapperMock.Object,
            authFactory,
            new ConsumptionService(),
            tariffServiceMock.Object,
            idTokenServiceMock.Object,
            transactionServiceMock.Object);

        return (sut, new Scenario(chargerServiceMock, transactionServiceMock, tariffServiceMock, call));
    }
}

/// <summary>Internal extension to extract the JSON payload from an <see cref="OcppCallResult"/> for assertions.</summary>
internal static class OcppCallResultExtensions
{
    public static T PayloadAs<T>(this OcppCallResult result) where T : class =>
        JsonSerializer.Deserialize<T>(result.Payload, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new Medinilla.Infrastructure.DottedEnumJsonConverter() },
        }) ?? throw new InvalidOperationException($"Failed to deserialize payload to {typeof(T).Name}");
}
