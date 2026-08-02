using Medinilla.DataAccess.Relational.Models;
using DbChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;

namespace Medinilla.Core.v1.Services;

public sealed class TariffService
{
    public decimal CalculateTotalCosts(float totalValue, DbChargingStation cs, string unit)
    {
        var unitPrice = !string.IsNullOrEmpty(unit)
            ? cs.Tariffs?.Where(t => t.UnitName == unit).FirstOrDefault()?.UnitPrice ?? 1.0M
            : 1.0M;
        return Convert.ToDecimal(totalValue) * unitPrice;
    }
}
