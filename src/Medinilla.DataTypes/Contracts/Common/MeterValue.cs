namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// Collection of one or more sampled values in MeterValues. All sampled values in
/// a MeterValue are sampled at the same point in time
/// </summary>
public class MeterValue
{
    /// <summary>
    /// Timestamp for measured value(s)
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// One or more measured values
    /// </summary>
    public List<SampledValue> SampledValue { get; set; }
}