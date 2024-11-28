using Microsoft.Extensions.DependencyInjection;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.UnitOfWork;

namespace Medinilla.DataAccess;

public static class ServiceCollectionExtensions
{
    public static void AddMedinillaDataAccess(this IServiceCollection services)
    {
        services.AddEntityFrameworkNpgsql();
        services.AddDbContext<MedinillaOcppDbContext>();
        services.AddTransient<TransactionsUnitOfWork>();
        services.AddTransient<ChargingStationUnitOfWork>();
    }
}
