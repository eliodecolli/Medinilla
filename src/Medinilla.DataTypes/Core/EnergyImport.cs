using Medinilla.DataTypes.Core.Enums;

namespace Medinilla.DataTypes.Core;

public sealed class EnergyImport
{
    public decimal EnergyImportValue { get; set; }
    public ConsumptionType ConsumptionType { get; set; }
}
