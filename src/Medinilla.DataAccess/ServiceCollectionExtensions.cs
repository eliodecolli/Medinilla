using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;

namespace Medinilla.DataAccess;

public static class ServiceCollectionExtensions
{
    public static void AddMedinillaDataAccess(this IServiceCollection services)
    {
        services.AddDbContext<MedinillaOcppDbContext>();

        services.AddTransient<TransactionsUnitOfWork>();
        services.AddTransient<ChargingStationUnitOfWork>();
    }
}
