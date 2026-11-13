using Medinilla.Core.v1.Services;
using Medinilla.DataAccess.Relational.Enums;
using Medinilla.DataAccess.Relational.Models;

namespace Medinilla.Core.Tests;

public class ConsumptionServiceShould
{
    private readonly ConsumptionService _service = new();

    [Fact]
    public async Task SubtractTheLatestBeginFromTheLatestEndRegardlessOfInputOrder()
    {
        var result = await _service.TryComputeConsumption(
        [
            Sample(ReadingContext.TransactionEnd, 150m, minute: 10),
            Sample(ReadingContext.TransactionBegin, 100m),
            Sample(ReadingContext.TransactionEnd, 140m, minute: 9),
            Sample(ReadingContext.TransactionBegin, 101m, minute: 1),
        ]);

        Assert.Equal(49m, result);
    }

    [Fact]
    public async Task UseTheLatestPeriodicReadingWhenEndIsAbsent()
    {
        var result = await _service.TryComputeConsumption(
        [
            Sample(ReadingContext.SamplePeriodic, 105000m, minute: 10),
            Sample(ReadingContext.TransactionBegin, 100000m),
            Sample(ReadingContext.SamplePeriodic, 102000m, minute: 5),
        ]);

        Assert.Equal(5000m, result);
    }

    [Fact]
    public async Task PreferEndOverPeriodicReadings()
    {
        var result = await _service.TryComputeConsumption(
        [
            Sample(ReadingContext.TransactionBegin, 100m),
            Sample(ReadingContext.TransactionEnd, 150m, minute: 10),
            Sample(ReadingContext.SamplePeriodic, 999m, minute: 11),
        ]);

        Assert.Equal(50m, result);
    }

    [Theory]
    [InlineData(MeasurementLocation.Inlet)]
    [InlineData(MeasurementLocation.Body)]
    [InlineData(MeasurementLocation.Cable)]
    [InlineData(MeasurementLocation.EV)]
    public async Task IgnoreReadingsOutsideTheOutlet(MeasurementLocation location)
    {
        var ignored = Sample(ReadingContext.TransactionEnd, 999m, minute: 11);
        ignored.Location = location;
        var result = await _service.TryComputeConsumption(
        [
            ignored,
            Sample(ReadingContext.TransactionBegin, 100m),
            Sample(ReadingContext.TransactionEnd, 150m, minute: 10),
        ]);

        Assert.Equal(50m, result);
    }

    [Fact]
    public async Task IgnoreIndividualPhaseReadings()
    {
        var phase = Sample(ReadingContext.TransactionEnd, 999m, minute: 11);
        phase.Phase = MeasurementPhase.L1;

        Assert.Equal(50m, await _service.TryComputeConsumption(
        [
            Sample(ReadingContext.TransactionBegin, 100m),
            Sample(ReadingContext.TransactionEnd, 150m, minute: 10),
            phase,
        ]));
        Assert.Null(await _service.TryComputeConsumption(
        [
            Sample(ReadingContext.TransactionBegin, 100m),
            phase,
        ]));
    }

    [Theory]
    [InlineData(Measurand.EnergyActiveImportInterval)]
    [InlineData(Measurand.EnergyActiveExportRegister)]
    [InlineData(Measurand.PowerActiveImport)]
    public async Task IgnoreUnsupportedMeasurands(Measurand measurand)
    {
        var begin = Sample(ReadingContext.TransactionBegin, 100m);
        var end = Sample(ReadingContext.TransactionEnd, 150m, minute: 10);
        begin.Measurand = end.Measurand = measurand;

        Assert.Null(await _service.TryComputeConsumption([begin, end]));
    }

    [Fact]
    public async Task ReturnNullWhenARequiredReadingIsMissing()
    {
        Assert.Null(await _service.TryComputeConsumption([]));
        Assert.Null(await _service.TryComputeConsumption([Sample(ReadingContext.TransactionBegin, 100m)]));
        Assert.Null(await _service.TryComputeConsumption([Sample(ReadingContext.TransactionEnd, 150m)]));
        Assert.Null(await _service.TryComputeConsumption([Sample(ReadingContext.SamplePeriodic, 120m)]));
    }

    [Fact]
    public async Task ReturnZeroWhenTheCounterHasNotChanged()
    {
        Assert.Equal(0m, await _service.TryComputeConsumption(
        [
            Sample(ReadingContext.TransactionBegin, 100m),
            Sample(ReadingContext.TransactionEnd, 100m, minute: 10),
        ]));
    }

    [Fact]
    public async Task PreserveDecimalPrecision()
    {
        const decimal begin = 1234567890123456.123456789m;
        var result = await _service.TryComputeConsumption(
        [
            Sample(ReadingContext.TransactionBegin, begin),
            Sample(ReadingContext.TransactionEnd, begin + 0.000000003m, minute: 10),
        ]);

        Assert.Equal(0.000000003m, result);
    }

    private static TransactionSample Sample(ReadingContext context, decimal value, int minute = 0)
    {
        return new TransactionSample
        {
            TransactionId = "transaction-1",
            Timestamp = DateTime.UnixEpoch.AddMinutes(minute),
            Context = context,
            Value = value,
            Location = MeasurementLocation.Outlet,
            Measurand = Measurand.EnergyActiveImportRegister,
        };
    }
}
