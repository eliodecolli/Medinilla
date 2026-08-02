using Medinilla.DataAccess.Relational.Models;
using DbChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;

namespace Medinilla.Core.Interfaces.Services;

public interface ITariffService
{
    decimal CalculateTotalCosts(float totalValue, DbChargingStation cs, string unit);
}
