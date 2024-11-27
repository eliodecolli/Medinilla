using Microsoft.Extensions.DependencyInjection;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.UnitOfWork;

namespace Medinilla.DataAccess;

public static class ServiceCollectionExtensions
{
    public static void AddMedinillaDataAccess(this IServiceCollection services)
    {
        services.AddDbContext<MedinillaOcppDbContext>();
        services.AddScoped<TransactionsUnitOfWork>();
    }
}
