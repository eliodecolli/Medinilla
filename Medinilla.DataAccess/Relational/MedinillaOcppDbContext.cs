using Medinilla.DataAccess.Relational.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Medinilla.DataAccess.Relational;

public class MedinillaOcppDbContext(IConfiguration config) : DbContext
{
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<ChargingStation> ChargingStations { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseNpgsql(config.GetConnectionString("MedinillaCore"));
    }
}
