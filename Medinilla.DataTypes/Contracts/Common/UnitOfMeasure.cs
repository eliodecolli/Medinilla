namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// Represents a UnitOfMeasure with a multiplier
/// </summary>
public class UnitOfMeasure
{
    /// <summary>
    /// Unit of the value. Default = "Wh" if the (default) measurand is an "Energy" type.
    /// This field SHALL use a value from the list Standardized Units of Measurements in Part 2 Appendices.
    /// If an applicable unit is available in that list, otherwise a "custom" unit might be used
    /// </summary>
    public string Unit { get; set; } = "Wh";

    /// <summary>
    /// Multiplier, this value represents the exponent to base 10. I.e. multiplier 3 means 10 raised to the 3rd power.
    /// Default is 0
    /// </summary>
    public int Multiplier { get; set; } = 0;
}