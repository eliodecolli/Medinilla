namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// Single sampled value in MeterValues. Each value can be accompanied by optional fields.
/// To save on mobile data usage, default values of all of the optional fields are such that
/// the value without any additional fields will be interpreted as a register reading of
/// active import energy in Wh (Watt-hour) units
/// </summary>
public class SampledValue
{
    /// <summary>
    /// Indicates the measured value
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// Type of detail value: start, end or sample. Default = "Sample.Periodic"
    /// </summary>
    public ReadingContextEnum? Context { get; set; }

    /// <summary>
    /// Type of measurement. Default = "Energy.Active.Import.Register"
    /// </summary>
    public MeasurandEnum? Measurand { get; set; }

    /// <summary>
    /// Indicates how the measured value is to be interpreted. For instance between L1 and neutral (L1-N).
    /// When phase is absent, the measured value is interpreted as an overall value
    /// </summary>
    public PhaseEnum? Phase { get; set; }

    /// <summary>
    /// Indicates where the measured value has been sampled. Default = "Outlet"
    /// </summary>
    public LocationEnum? Location { get; set; }

    /// <summary>
    /// Represents a signed version of the meter value
    /// </summary>
    public SignedMeterValue SignedMeterValue { get; set; }

    /// <summary>
    /// Represents a UnitOfMeasure with a multiplier
    /// </summary>
    public UnitOfMeasure UnitOfMeasure { get; set; }
}