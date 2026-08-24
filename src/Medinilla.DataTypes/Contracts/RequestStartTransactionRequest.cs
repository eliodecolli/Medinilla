using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

public sealed class RequestStartTransactionRequest
{
    public int? EvseId { get; set; }

    public IdToken? GroupIdToken { get; set; }

    public IdToken IdToken { get; set; }

    public int RemoteStartId { get; set; }
}