using Medinilla.DataTypes.Core.Enums;

namespace Medinilla.DataTypes.Core;

public sealed class TransactionConsumption
{
    public ConsumptionType ConsumptionType { get; set; }

    public float Consumption { get; set; }

    public DateTime Timestamp { get; set; }
}
