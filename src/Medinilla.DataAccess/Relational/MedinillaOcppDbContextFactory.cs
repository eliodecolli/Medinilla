using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Medinilla.DataAccess.Relational;

public class MedinillaOcppDbContextFactory : IDesignTimeDbContextFactory<MedinillaOcppDbContext>
{
    public MedinillaOcppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MEDINILLA_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=medinilla;Username=medinilla;Password=medinilla";

        var optionsBuilder = new DbContextOptionsBuilder<MedinillaOcppDbContext>();
        optionsBuilder.UseLazyLoadingProxies();
        optionsBuilder.UseNpgsql(connectionString, b => b.MigrationsAssembly("Medinilla.DataAccess"));

        return new MedinillaOcppDbContext(optionsBuilder.Options);
    }
}
