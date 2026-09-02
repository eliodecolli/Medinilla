using Google.Protobuf.WellKnownTypes;
using Medinilla.DataAccess.Relational.Models;
using ProtoSnapshot = Medinilla.Core.gRPC.Query.TransactionSnapshot;

namespace Medinilla.Core.Service.Communication.Mapping;

public partial class MedinillaMapping
{
    public static ProtoSnapshot MapTransactionSnapshot(TransactionSnapshot snap) => new()
    {
        TransactionId = snap.TransactionId,
        ClientIdentifier = snap.ChargingStation?.ClientIdentifier ?? string.Empty,
        EvseId = snap.EvseConnector?.EvseId ?? 0,
        ConnectorId = snap.EvseConnector?.ConnectorId ?? 0,
        IdToken = snap.IdToken?.Token ?? string.Empty,
        StartReason = snap.StartReason,
        EndReason = snap.EndReason,
        TotalMeteredValue = (double)snap.TotalMeteredValue,
        TotalCost = (double)snap.TotalCost,
        StartedAt = Timestamp.FromDateTime(ToUtc(snap.StartedAt)),
        EndedAt = Timestamp.FromDateTime(ToUtc(snap.EndedAt)),
    };
}
