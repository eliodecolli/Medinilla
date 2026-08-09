using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Audit;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Medinilla.DataAccess.Relational.Models.ChargerConfig;
using Microsoft.EntityFrameworkCore;

namespace Medinilla.DataAccess.Relational;

public class MedinillaOcppDbContext(DbContextOptions<MedinillaOcppDbContext> options) : DbContext(options)
{

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // configure table
        modelBuilder.Entity<Account>().ToTable("core_account");
        modelBuilder.Entity<ChargingStation>().ToTable("core_charging_station");
        modelBuilder.Entity<EvseConnector>().ToTable("core_evse_connector");
        modelBuilder.Entity<Tariff>().ToTable("core_tariff");
        modelBuilder.Entity<TransactionEvent>().ToTable("core_transactions_event");
        modelBuilder.Entity<TransactionSnapshot>().ToTable("core_transactions_snapshot");
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

        // configure account
        modelBuilder.Entity<Account>().HasMany(c => c.ChargingStations)
            .WithOne(c => c.Account)
            .HasForeignKey(c => c.AccountId);

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

        // configure transaction events
        modelBuilder.Entity<TransactionEvent>().Property(c => c.ConsumptionType)
            .HasConversion<string>();

        modelBuilder.Entity<TransactionEvent>().HasOne(c => c.IdToken)
            .WithMany(i => i.TransactionEvents)
            .HasForeignKey(c => c.IdTokenId);

        // configure transaction snapshots
        modelBuilder.Entity<TransactionSnapshot>().HasOne(c => c.EvseConnector)
            .WithMany()
            .HasForeignKey(c => c.EvseConnectorId);

        // configure auth
        modelBuilder.Entity<IdToken>().HasOne(c => c.ChargingStation)
            .WithMany(c => c.IdTokens)
            .HasForeignKey(c => c.ChargingStationId);

        modelBuilder.Entity<IdToken>().HasOne(c => c.User)
            .WithMany(c => c.Tokens)
            .HasForeignKey(c => c.AuthorizationUserId);

        modelBuilder.Entity<IdToken>().Property(c => c.Blocked)
            .HasDefaultValue(false);

        modelBuilder.Entity<IdToken>().HasMany(c => c.TransactionSnapshots)
            .WithOne(c => c.IdToken)
            .HasForeignKey(c => c.IdTokenId);

        // configure command execution auditing
        modelBuilder.Entity<CommandExecution>().ToTable("core_command_executions");
        modelBuilder.Entity<CommandExecution>().HasIndex(ce => ce.MessageId);
        
        // configure charger configs
        // report status
        modelBuilder.Entity<ReportBaseStatus>().ToTable("core_report_base_statuses");
        modelBuilder.Entity<ReportBaseStatus>().HasIndex(r => r.RequestId);
        modelBuilder.Entity<ReportBaseStatus>().HasIndex(r => new { r.RequestId, r.SeqNo })
            .IsUnique();
        
        // charger component
        modelBuilder.Entity<ChargerComponent>().ToTable("core_charger_components");
        modelBuilder.Entity<ChargerComponent>().HasIndex(cc => cc.ClientIdentifier);
        modelBuilder.Entity<ChargerComponent>().HasIndex(cc => new { cc.ComponentName, cc.ComponentInstance });
        modelBuilder.Entity<ChargerComponent>().HasOne(cc => cc.Connector)
            .WithMany(ec => ec.Components)
            .HasForeignKey(cc => cc.EvseConnectorId);
        modelBuilder.Entity<ChargerComponent>().HasOne(cc => cc.ChargingStation)
            .WithMany()
            .HasForeignKey(cc => cc.ChargingStationId);

        // component variable
        modelBuilder.Entity<ComponentVariable>().ToTable("core_component_variables");
        modelBuilder.Entity<ComponentVariable>().HasIndex(cc => new { cc.Name, cc.AttributeType });
        modelBuilder.Entity<ComponentVariable>().HasIndex(cc => new { cc.Name, cc.Instance });
        
        modelBuilder.Entity<ComponentVariable>().Property(cv => cv.AttributeType)
            .HasConversion<string>();
        modelBuilder.Entity<ComponentVariable>().Property(cv => cv.Mutability)
            .HasConversion<string>();
        modelBuilder.Entity<ComponentVariable>().HasOne(cv => cv.Component)
            .WithMany(cc => cc.ComponentVariables)
            .HasForeignKey(cv => cv.ChargerComponentId);
    }
}
