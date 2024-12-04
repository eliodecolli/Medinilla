using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Medinilla.DataTypes.Core.Authorization;
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
        modelBuilder.Entity<IdToken>().ToTable("core_id_token");
        modelBuilder.Entity<AuthorizationDetails>().ToTable("core_auth_details");
        modelBuilder.Entity<AuthorizationUser>().ToTable("core_auth_user");

        // configure indecies
        modelBuilder.Entity<TransactionSnapshot>().HasIndex(c => new { c.ChargingStationId, c.TransactionId });
        modelBuilder.Entity<TransactionSnapshot>().HasIndex(c => c.TransactionId);

        modelBuilder.Entity<TransactionEvent>().HasIndex(c => new { c.ChargingStationId, c.TransactionId });
        modelBuilder.Entity<TransactionEvent>().HasIndex(c => c.TransactionId);
        modelBuilder.Entity<TransactionEvent>().HasIndex(c => c.SeqNo);
        modelBuilder.Entity<TransactionEvent>().HasIndex(c => c.EventType);

        modelBuilder.Entity<IdToken>().HasIndex(c => new { c.ChargingStationId, c.Token });

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

        modelBuilder.Entity<ChargingStation>().HasOne(c => c.AuthorizationDetails)
            .WithOne(c => c.ChargingStation)
            .HasForeignKey<AuthorizationDetails>(c => c.ChargingStationId);

        // configure transaction snapshots
        modelBuilder.Entity<TransactionSnapshot>().HasOne(c => c.EvseConnector)
            .WithMany()
            .HasForeignKey(c =>c.EvseConnectorId);

        // configure auth
        modelBuilder.Entity<IdToken>().HasOne(c => c.ChargingStation)
            .WithMany()
            .HasForeignKey(c => c.ChargingStationId);

        modelBuilder.Entity<IdToken>().HasOne(c => c.User)
            .WithMany(c => c.Tokens)
            .HasForeignKey(c => c.AuthorizationUserId);

        modelBuilder.Entity<IdToken>().Property(c => c.Blocked)
            .HasDefaultValue(false);

        modelBuilder.Entity<IdToken>().HasMany(c => c.TransactionSnapshots)
            .WithOne()
            .HasForeignKey(c => c.IdTokenId);

        modelBuilder.Entity<IdToken>().HasMany(c => c.TransactionEvents)
            .WithOne()
            .HasForeignKey(c => c.IdTokenId);

        modelBuilder.Entity<AuthorizationDetails>().Property(c => c.AuthBlob)
            .HasColumnType("json");
    }
}
