using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Medinilla.DataAccess;

public static class ServiceCollectionExtensions
{
    public static void AddMedinillaDataAccess(this IServiceCollection services)
    {
        services.AddDbContext<MedinillaOcppDbContext>((sp, options) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            options.UseLazyLoadingProxies();
            options.UseNpgsql(config.GetConnectionString("MedinillaCore"), b => b.MigrationsAssembly("Medinilla.DataAccess"));
        });

        services.AddTransient<TransactionsUnitOfWork>();
        services.AddTransient<ChargingStationUnitOfWork>();
        services.AddTransient<CommandExecutionUnitOfWork>();
    }
}
