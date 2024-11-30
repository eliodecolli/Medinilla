using Medinilla.DataAccess.Relational.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Medinilla.DataAccess.Relational;

public class MedinillaOcppDbContext(IConfiguration config) : DbContext
{
    public DbSet<TransactionEvent> TransactionEvents { get; set; }
    public DbSet<ChargingStation> ChargingStations { get; set; }
    public DbSet<EvseConnector> EvseConnectors { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseLazyLoadingProxies();
        optionsBuilder.UseNpgsql(config.GetConnectionString("MedinillaCore"));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // configure table
        modelBuilder.Entity<ChargingStation>().ToTable("charging_station");
        modelBuilder.Entity<EvseConnector>().ToTable("evse_connector");
        modelBuilder.Entity<Tariff>().ToTable("tariff");
        modelBuilder.Entity<TransactionEvent>().ToTable("transactions_event");
        modelBuilder.Entity<TransactionSnapshot>().ToTable("transactions_snapshot");

        // configure indecies
        modelBuilder.Entity<TransactionSnapshot>().HasIndex(c => new { c.ChargingStationId, c.TransactionId });
        modelBuilder.Entity<TransactionSnapshot>().HasIndex(c => c.TransactionId);

        modelBuilder.Entity<TransactionEvent>().HasIndex(c => new { c.ChargingStationId, c.TransactionId });
        modelBuilder.Entity<TransactionEvent>().HasIndex(c => c.TransactionId);
        modelBuilder.Entity<TransactionEvent>().HasIndex(c => c.SeqNo);
        modelBuilder.Entity<TransactionEvent>().HasIndex(c => c.EventType);

        modelBuilder.Entity<ChargingStation>().HasIndex(c => c.ClientIdentifier);

        // configure charging station
        modelBuilder.Entity<ChargingStation>().HasMany(c => c.EvseConnectors)
            .WithOne(c => c.ChargingStation)
            .HasForeignKey(c => c.ChargingStationId);

        modelBuilder.Entity<ChargingStation>().HasMany(c => c.TransactionEvents)
            .WithOne(c => c.ChargingStation)
            .HasForeignKey(c => c.ChargingStationId);

        modelBuilder.Entity<ChargingStation>().HasMany(c => c.TransactionSnapshots)
            .WithOne(c => c.ChargingStation)
            .HasForeignKey(c => c.ChargingStationId);

        modelBuilder.Entity<ChargingStation>().HasMany(c => c.Tariffs)
            .WithOne(c => c.ChargingStation)
            .HasForeignKey(c => c.ChargingStationId);

        // configure transaction snapshots
        modelBuilder.Entity<TransactionSnapshot>().HasOne(c => c.EvseConnector)
            .WithMany()
            .HasForeignKey(c =>c.EvseConnectorId);
    }
}
