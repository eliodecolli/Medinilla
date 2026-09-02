using Google.Protobuf.WellKnownTypes;
using Medinilla.Core.gRPC.Query;
using Medinilla.DataAccess.Relational.Models;

namespace Medinilla.Core.Service.Communication.Mapping;

public partial class MedinillaMapping
{
    public static Charger MapCharger(ChargingStation cs) => new()
    {
        ClientIdentifier = cs.ClientIdentifier,
        Vendor = cs.Vendor,
        Model = cs.Model,
        Booted = cs.Booted,
        LatestBootNotificationReason = cs.LatestBootNotificationReason,
        CreatedAt = Timestamp.FromDateTime(ToUtc(cs.CreatedAt)),
        ModifiedAt = cs.ModifiedAt.HasValue
            ? Timestamp.FromDateTime(ToUtc(cs.ModifiedAt.Value))
            : null,
        AccountId = cs.AccountId.ToString(),
        Alias = cs.Alias ?? string.Empty,
        Location = cs.Location ?? string.Empty,
        Connectors = { cs.EvseConnectors.Select(MapConnector) },
    };

    private static Connector MapConnector(EvseConnector ec) => new()
    {
        EvseId = ec.EvseId,
        ConnectorId = ec.ConnectorId,
        ConnectorStatus = ec.ConnectorStatus,
        ModifiedAt = Timestamp.FromDateTime(ToUtc(ec.ModifiedAt)),
    };

    private static DateTime ToUtc(DateTime dt) =>
        DateTime.SpecifyKind(dt, DateTimeKind.Utc);
}
