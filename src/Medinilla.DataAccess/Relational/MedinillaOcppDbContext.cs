using Medinilla.DataAccess.Exceptions;
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
        modelBuilder.Entity<IdToken>().ToTable("core_id_token");
        modelBuilder.Entity<AuthorizationDetails>().ToTable("core_auth_details");
        modelBuilder.Entity<AuthorizationUser>().ToTable("core_auth_user");

        // configure indecies
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

        modelBuilder.Entity<ChargingStation>().HasMany(c => c.Tariffs)
            .WithOne(c => c.ChargingStation)
            .HasForeignKey(c => c.ChargingStationId);

        modelBuilder.Entity<ChargingStation>().HasOne(c => c.AuthorizationDetails)
            .WithOne(c => c.ChargingStation)
            .HasForeignKey<AuthorizationDetails>(c => c.ChargingStationId);

        // configure auth
        modelBuilder.Entity<IdToken>().HasOne(c => c.ChargingStation)
            .WithMany(c => c.IdTokens)
            .HasForeignKey(c => c.ChargingStationId);

        modelBuilder.Entity<IdToken>().HasOne(c => c.User)
            .WithMany(c => c.Tokens)
            .HasForeignKey(c => c.AuthorizationUserId);

        modelBuilder.Entity<IdToken>().Property(c => c.Blocked)
            .HasDefaultValue(false);

        modelBuilder.Entity<AuthorizationUser>().HasOne(c => c.ChargingStation)
            .WithMany(c => c.AuthorizationUsers)
            .HasForeignKey(c => c.ChargingStationId);

        modelBuilder.Entity<TransactionEvent>().ToTable("core_transactions_event");
        modelBuilder.Entity<TransactionEvent>().HasIndex(te => new
        {
            te.ChargingStationId,
            te.TransactionId,
            te.SeqNo
        }).IsUnique();

        modelBuilder.Entity<TransactionEvent>().Property(te => te.EventType)
            .HasConversion<string>();
        modelBuilder.Entity<TransactionEvent>().Property(te => te.TriggerReason)
            .HasConversion<string>();
        modelBuilder.Entity<TransactionEvent>().Property(te => te.ChargingState)
            .HasConversion<string>();
        modelBuilder.Entity<TransactionEvent>().Property(te => te.StoppedReason)
            .HasConversion<string>();

        modelBuilder.Entity<TransactionEvent>().HasOne(te => te.ChargingStation)
            .WithMany(cs => cs.TransactionEvents)
            .HasForeignKey(te => te.ChargingStationId);
        modelBuilder.Entity<TransactionEvent>().HasOne(te => te.IdToken)
            .WithMany()
            .HasForeignKey(te => te.IdTokenId)
            .OnDelete(DeleteBehavior.SetNull);

        // configure transaction samples
        modelBuilder.Entity<TransactionSample>().ToTable("core_transaction_samples");
        modelBuilder.Entity<TransactionSample>().Property(ts => ts.Measurand)
            .HasConversion<string>();
        modelBuilder.Entity<TransactionSample>().Property(ts => ts.Location)
            .HasConversion<string>();
        modelBuilder.Entity<TransactionSample>().Property(ts => ts.Context)
            .HasConversion<string>();
        modelBuilder.Entity<TransactionSample>().Property(ts => ts.Phase)
            .HasConversion<string>();
        modelBuilder.Entity<TransactionSample>().HasOne(ts => ts.TransactionEvent)
            .WithMany(te => te.Samples)
            .HasForeignKey(ts => ts.TransactionEventId);

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

    public async Task<ChargingStation> GetChargingStation(string clientIdentifier)
    {
        var result = await Set<ChargingStation>()
            .FirstOrDefaultAsync(c => c.ClientIdentifier == clientIdentifier)
            .ConfigureAwait(false);

        if (result is null)
            throw new AggregateRootNotFoundException($"Cannot find charging station for client {clientIdentifier}");

        return result;
    }
}
