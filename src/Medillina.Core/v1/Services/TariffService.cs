using Medinilla.Core.Interfaces.Services;
using DbChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;

namespace Medinilla.Core.v1.Services;

public class TariffService : ITariffService
{
    public decimal CalculateTotalCosts(float totalValue, DbChargingStation cs, string unit)
    {
        var unitPrice = !string.IsNullOrEmpty(unit)
            ? cs.Tariffs?.Where(t => t.UnitName == unit).FirstOrDefault()?.UnitPrice ?? 1.0M
            : 1.0M;
        return Convert.ToDecimal(totalValue) * unitPrice;
    }
}
