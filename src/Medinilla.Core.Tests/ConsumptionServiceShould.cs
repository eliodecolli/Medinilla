using Medinilla.Core.v1.Transactions;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Enums;

namespace Medinilla.Core.Tests;

/// <summary>
/// Consumption-calculation edge cases for <see cref="ConsumptionService"/>.
///
/// The service consumes the <c>meterValue</c> array that arrives in
/// <c>TransactionEventRequest</c> (OCPP 2.0.1 §2.41 SampledValueType, page 398):
/// each <c>SampledValue</c> carries <c>context</c> (<c>Transaction.Begin</c>,
/// <c>Transaction.End</c>, <c>Sample.Periodic</c>, etc.), <c>measurand</c>
/// (Register vs Interval), <c>phase</c> (L1/L2/L3, or absent for an overall reading)
/// and <c>unitOfMeasure</c>. The CSMS receives whatever the charging station sends
/// (see §2.7 SampledDataTxStartedMeasurands et al., pages 464-465) and these tests
/// pin down the resulting math so future refactors cannot silently regress it.
/// </summary>
public class ConsumptionServiceShould
{
    private readonly ConsumptionService _service = new();

    // -- 1. Happy path: register readings only, one overall reading ---------------

    [Fact]
    public void CalculateConsumption_FromOverallRegisterReadings()
    {
        // Tx.Begin total = 100 Wh, Tx.End total = 151 Wh -> 51 Wh consumed.
        var meters = new[]
        {
            MeterValueAt(begin, BeginSample(MeasurandEnum.EnergyActiveImportRegister, value: 100m, unit: "Wh")),
            MeterValueAt(end, EndSample(MeasurandEnum.EnergyActiveImportRegister, value: 151m, unit: "Wh")),
        };

        var consumption = _service.GetTransactionConsumption(meters);

        Assert.Equal(51f, consumption.Consumption);
        Assert.Equal(ConsumptionType.Cumulative, consumption.ConsumptionType);
    }

    // -- 2. Register readings with phase breakdown (no overall reading) -----------

    [Fact]
    public void CalculateConsumption_FromPhaseRegisterReadings_SumsL1L2L3()
    {
        // Per OCPP §2.41: "When phase is absent, the measured value is interpreted as an
        // overall value." So a charger that only sends L1/L2/L3 has no Outlet node, and
        // MeasurandNode.Compute() falls back to summing the phases.
        var beginPhases = new[]
        {
            BeginSample(MeasurandEnum.EnergyActiveImportRegister, phase: PhaseEnum.L1, value: 10m),
            BeginSample(MeasurandEnum.EnergyActiveImportRegister, phase: PhaseEnum.L2, value: 20m),
            BeginSample(MeasurandEnum.EnergyActiveImportRegister, phase: PhaseEnum.L3, value: 30m),
        };
        var endPhases = new[]
        {
            EndSample(MeasurandEnum.EnergyActiveImportRegister, phase: PhaseEnum.L1, value: 25m),
            EndSample(MeasurandEnum.EnergyActiveImportRegister, phase: PhaseEnum.L2, value: 50m),
            EndSample(MeasurandEnum.EnergyActiveImportRegister, phase: PhaseEnum.L3, value: 75m),
        };

        var meters = new[]
        {
            MeterValueAt(begin, beginPhases),
            MeterValueAt(end, endPhases),
        };

        var consumption = _service.GetTransactionConsumption(meters);

        // 150 - 60 = 90 Wh
        Assert.Equal(90f, consumption.Consumption);
    }

    // -- 3. Register readings with both overall and per-phase ---------------------

    [Fact]
    public void CalculateConsumption_PrefersOverallReadingOverPhaseSum()
    {
        // When the charger sends both an overall Outlet value AND per-phase values, the
        // overall reading wins (MeasurandNode.Compute() prefers Outlet over Phases).
        var beginMixed = new SampledValue[]
        {
            new SampledValueBuilder().Context(ReadingContextEnum.TransactionBegin).Measurand(MeasurandEnum.EnergyActiveImportRegister).Value(100m).Unit("Wh").Build(),
            new SampledValueBuilder().Context(ReadingContextEnum.TransactionBegin).Measurand(MeasurandEnum.EnergyActiveImportRegister).Phase(PhaseEnum.L1).Value(40m).Unit("Wh").Build(),
            new SampledValueBuilder().Context(ReadingContextEnum.TransactionBegin).Measurand(MeasurandEnum.EnergyActiveImportRegister).Phase(PhaseEnum.L2).Value(40m).Unit("Wh").Build(),
            new SampledValueBuilder().Context(ReadingContextEnum.TransactionBegin).Measurand(MeasurandEnum.EnergyActiveImportRegister).Phase(PhaseEnum.L3).Value(40m).Unit("Wh").Build(),
        };
        var endMixed = new SampledValue[]
        {
            new SampledValueBuilder().Context(ReadingContextEnum.TransactionEnd).Measurand(MeasurandEnum.EnergyActiveImportRegister).Value(100m + 51m).Unit("Wh").Build(),
            new SampledValueBuilder().Context(ReadingContextEnum.TransactionEnd).Measurand(MeasurandEnum.EnergyActiveImportRegister).Phase(PhaseEnum.L1).Value(40m + 17m).Unit("Wh").Build(),
            new SampledValueBuilder().Context(ReadingContextEnum.TransactionEnd).Measurand(MeasurandEnum.EnergyActiveImportRegister).Phase(PhaseEnum.L2).Value(40m + 17m).Unit("Wh").Build(),
            new SampledValueBuilder().Context(ReadingContextEnum.TransactionEnd).Measurand(MeasurandEnum.EnergyActiveImportRegister).Phase(PhaseEnum.L3).Value(40m + 17m).Unit("Wh").Build(),
        };

        var meters = new[]
        {
            MeterValueAt(begin, beginMixed),
            MeterValueAt(end, endMixed),
        };

        var consumption = _service.GetTransactionConsumption(meters);

        Assert.Equal(51f, consumption.Consumption);
    }

    // -- 4. Register readings + interval readings: register takes precedence -------

    [Fact]
    public void RegisterReadings_TakePrecedenceOverIntervalReadings_WhenBothPresent()
    {
        // TxGraph.Compute(): Register ?? Interval. If a charger sends both Register and
        // Interval measurands, the cumulative Register wins. Otherwise chargers that only
        // send Interval (e.g. certain DC fast chargers) get the interval path.
        var meters = new[]
        {
            MeterValueAt(begin,
                BeginSample(MeasurandEnum.EnergyActiveImportRegister, value: 100m, unit: "Wh"),
                BeginSample(MeasurandEnum.EnergyActiveImportInterval, value: 5m, unit: "Wh")),
            MeterValueAt(end,
                EndSample(MeasurandEnum.EnergyActiveImportRegister, value: 200m, unit: "Wh"),
                EndSample(MeasurandEnum.EnergyActiveImportInterval, value: 9m, unit: "Wh")),
        };

        var consumption = _service.GetTransactionConsumption(meters);

        // Register path: 200 - 100 = 100. (The interval samples contribute only if register
        // is absent, so they must NOT influence the result here.)
        Assert.Equal(100f, consumption.Consumption);
    }

    // -- 5. Interval-only charger: only Sample.Periodic readings -------------------

    [Fact]
    public void CalculateConsumption_FromIntervalSamplesOnly_WhenNoTransactionEndpoints()
    {
        // Some chargers never emit Tx.Begin/Tx.End and just send Sample.Periodic deltas
        // (e.g. mobile chargers, OCPP-compliant smart plugs). For those, consumption is
        // the sum of the interval values across the whole MeterValue list.
        var meters = new[]
        {
            new MeterValue
            {
                Timestamp = end,
                SampledValue = new List<SampledValue>
                {
                    Sample(measurand: MeasurandEnum.EnergyActiveImportInterval, context: ReadingContextEnum.SamplePeriodic, value: 7m),
                    Sample(measurand: MeasurandEnum.EnergyActiveImportInterval, context: ReadingContextEnum.SamplePeriodic, value: 11m),
                    Sample(measurand: MeasurandEnum.EnergyActiveImportInterval, context: ReadingContextEnum.SamplePeriodic, value: 13m),
                },
            },
        };

        var consumption = _service.GetTransactionConsumption(meters);

        Assert.Equal(31f, consumption.Consumption);
    }

    // -- 6. Multiple MeterValue entries in one request -----------------------------

    [Fact]
    public void MultipleMeterValueEntriesInOneRequest_AllContributedToGraph()
    {
        // OCPP §2.32 MeterValueType: a TransactionEventRequest may carry several MeterValue
        // entries — typically one per timestamp or per EVSE. All must end up in the graph.
        // We mix measurands (Register + Voltage) so each entry contributes independently
        // and we don't fall into the corner-case where two Tx.End Register samples sum up.
        var meters = new[]
        {
            MeterValueAt(begin, BeginSample(MeasurandEnum.EnergyActiveImportRegister, value: 100m, unit: "Wh")),
            MeterValueAt(end, EndSample(MeasurandEnum.EnergyActiveImportRegister, value: 130m, unit: "Wh")),
            MeterValueAt(end, EndSample(MeasurandEnum.Voltage, phase: PhaseEnum.L1, value: 230m, unit: "V")),
            MeterValueAt(end, EndSample(MeasurandEnum.Voltage, phase: PhaseEnum.L2, value: 230m, unit: "V")),
            MeterValueAt(end, EndSample(MeasurandEnum.Voltage, phase: PhaseEnum.L3, value: 230m, unit: "V")),
        };

        var consumption = _service.GetTransactionConsumption(meters);

        // Register wins (Register ?? Interval); the extra Voltage MeterValues contribute
        // to graph.End.Interval but do not change the Register-based consumption.
        Assert.Equal(30f, consumption.Consumption);
    }

    // -- 7. Tx.End without a Tx.Begin: result equals End reading -------------------

    [Fact]
    public void TxEndWithoutTxBegin_ReportsEndReading()
    {
        // Edge case per OCPP §1.5: optional fields may be omitted. A charger that sends
        // only an Ended event with Tx.End readings (and no Tx.Begin) yields the End value
        // as the consumption. The action layer still uses this for tariff computation.
        var meters = new[]
        {
            MeterValueAt(end, EndSample(MeasurandEnum.EnergyActiveImportRegister, value: 51m, unit: "Wh")),
        };

        var consumption = _service.GetTransactionConsumption(meters);

        Assert.Equal(51f, consumption.Consumption);
    }

    // -- 8. Zero consumption when End equals Begin ---------------------------------

    [Fact]
    public void ZeroConsumption_WhenEndRegisterEqualsBegin()
    {
        var meters = new[]
        {
            MeterValueAt(begin, BeginSample(MeasurandEnum.EnergyActiveImportRegister, value: 100m, unit: "Wh")),
            MeterValueAt(end, EndSample(MeasurandEnum.EnergyActiveImportRegister, value: 100m, unit: "Wh")),
        };

        var consumption = _service.GetTransactionConsumption(meters);

        Assert.Equal(0f, consumption.Consumption);
    }

    // -- 9. Negative consumption when End is below Begin ---------------------------

    [Fact]
    public void NegativeConsumption_WhenEndRegisterLessThanBegin()
    {
        // Can happen on meter reset, exported energy scenarios, or a buggy charger. The
        // service does not clamp to zero — the resulting negative value flows into billing.
        var meters = new[]
        {
            MeterValueAt(begin, BeginSample(MeasurandEnum.EnergyActiveImportRegister, value: 100m, unit: "Wh")),
            MeterValueAt(end, EndSample(MeasurandEnum.EnergyActiveImportRegister, value: 80m, unit: "Wh")),
        };

        var consumption = _service.GetTransactionConsumption(meters);

        Assert.Equal(-20f, consumption.Consumption);
    }

    // -- 10. Unit multiplier is currently ignored (regression guard) ---------------

    [Fact]
    public void MultiplierOnUnitOfMeasure_IsCurrentlyIgnored()
    {
        // OCPP §2.41: UnitOfMeasureType has a multiplier field (default 0). A charger that
        // sends 51 with multiplier=3 is reporting 51 kWh. The current implementation in
        // ConsumptionService.ScaleToKW() takes the raw value as-is (scaling is commented
        // out — see the source). This test pins that behaviour so any change to apply the
        // multiplier forces a deliberate update to the billing pipeline.
        var meters = new[]
        {
            MeterValueAt(begin, new SampledValueBuilder()
                .Context(ReadingContextEnum.TransactionBegin)
                .Measurand(MeasurandEnum.EnergyActiveImportRegister)
                .Value(0m)
                .Unit("kWh")
                .Multiplier(3)
                .Build()),
            MeterValueAt(end, new SampledValueBuilder()
                .Context(ReadingContextEnum.TransactionEnd)
                .Measurand(MeasurandEnum.EnergyActiveImportRegister)
                .Value(51m)
                .Unit("kWh")
                .Multiplier(3)
                .Build()),
        };

        var consumption = _service.GetTransactionConsumption(meters);

        // Current behaviour: 51, not 0.051.
        Assert.Equal(51f, consumption.Consumption);
    }

    // -- 11. Empty / null payload ---------------------------------------------------

    [Fact]
    public void NullMeterValues_ReturnsZeroConsumption_WithPeriodicType()
    {
        var consumption = _service.GetTransactionConsumption((IEnumerable<MeterValue>?)null);

        Assert.Equal(0f, consumption.Consumption);
        Assert.Equal(ConsumptionType.Periodic, consumption.ConsumptionType);
    }

    [Fact]
    public void EmptyMeterValues_ReturnsZeroConsumption_WithPeriodicType()
    {
        var consumption = _service.GetTransactionConsumption(Array.Empty<MeterValue>());

        Assert.Equal(0f, consumption.Consumption);
        Assert.Equal(ConsumptionType.Periodic, consumption.ConsumptionType);
    }

    // -- 12. Merging consumption graphs (incremental transaction reporting) ----------

    [Fact]
    public void MergingGraphs_AccumulatesConsumptionAcrossUpdates()
    {
        // A long transaction may arrive in pieces: an Updated event with samples, then a
        // later one. ConsumptionService exposes GetConsumptionGraph to build each piece and
        // the << operator (TxGraph) to merge. The merged graph should report the total
        // consumption across both windows.
        var firstWindow = new[]
        {
            MeterValueAt(begin, BeginSample(MeasurandEnum.EnergyActiveImportRegister, value: 100m, unit: "Wh")),
            MeterValueAt(end,   EndSample(MeasurandEnum.EnergyActiveImportRegister, value: 130m, unit: "Wh")),
        };
        var secondWindow = new[]
        {
            MeterValueAt(begin, BeginSample(MeasurandEnum.EnergyActiveImportRegister, value: 130m, unit: "Wh")),
            MeterValueAt(end,   EndSample(MeasurandEnum.EnergyActiveImportRegister, value: 151m, unit: "Wh")),
        };

        var firstGraph = _service.GetConsumptionGraph(firstWindow);
        var secondGraph = _service.GetConsumptionGraph(secondWindow);

        var merged = firstGraph << secondGraph;
        var consumption = _service.GetTransactionConsumption(merged!);

        // First window: 30. Second window: 21. Total: 51.
        Assert.Equal(51f, consumption.Consumption);
    }

    // ===========================================================================================
    // Meter-value builders
    // ===========================================================================================

    private static readonly DateTime begin = new(2025, 3, 6, 0, 11, 18, DateTimeKind.Utc);
    private static readonly DateTime end = new(2025, 3, 6, 0, 11, 40, DateTimeKind.Utc);

    private static MeterValue MeterValueAt(DateTime timestamp, params SampledValue[] samples) => new()
    {
        Timestamp = timestamp,
        SampledValue = new List<SampledValue>(samples),
    };

    private static MeterValue MeterValueAt(DateTime timestamp, IEnumerable<SampledValue> samples) => new()
    {
        Timestamp = timestamp,
        SampledValue = new List<SampledValue>(samples),
    };

    private static SampledValue BeginSample(
        MeasurandEnum measurand,
        PhaseEnum? phase = null,
        decimal value = 0m,
        string? unit = "Wh") =>
        Sample(measurand, ReadingContextEnum.TransactionBegin, phase, value, unit);

    private static SampledValue EndSample(
        MeasurandEnum measurand,
        PhaseEnum? phase = null,
        decimal value = 0m,
        string? unit = "Wh") =>
        Sample(measurand, ReadingContextEnum.TransactionEnd, phase, value, unit);

    private static SampledValue Sample(
        MeasurandEnum measurand,
        ReadingContextEnum context,
        PhaseEnum? phase = null,
        decimal value = 0m,
        string? unit = "Wh") => new()
    {
        Value = value,
        Context = context,
        Measurand = measurand,
        Phase = phase,
        Location = LocationEnum.Outlet,
        UnitOfMeasure = new UnitOfMeasure { Unit = unit ?? "Wh" },
    };

    /// <summary>Fluent builder for SampledValue so the tests read like the OCPP JSON schema.</summary>
    private sealed class SampledValueBuilder
    {
        private decimal _value;
        private ReadingContextEnum? _context;
        private MeasurandEnum? _measurand;
        private PhaseEnum? _phase;
        private string _unit = "Wh";
        private int _multiplier;

        public SampledValueBuilder Value(decimal value) { _value = value; return this; }
        public SampledValueBuilder Context(ReadingContextEnum context) { _context = context; return this; }
        public SampledValueBuilder Measurand(MeasurandEnum measurand) { _measurand = measurand; return this; }
        public SampledValueBuilder Phase(PhaseEnum phase) { _phase = phase; return this; }
        public SampledValueBuilder Unit(string unit) { _unit = unit; return this; }
        public SampledValueBuilder Multiplier(int multiplier) { _multiplier = multiplier; return this; }

        public SampledValue Build() => new()
        {
            Value = _value,
            Context = _context,
            Measurand = _measurand,
            Phase = _phase,
            Location = LocationEnum.Outlet,
            UnitOfMeasure = new UnitOfMeasure { Unit = _unit, Multiplier = _multiplier },
        };
    }
}