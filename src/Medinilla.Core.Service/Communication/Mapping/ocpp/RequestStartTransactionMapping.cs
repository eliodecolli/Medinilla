using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.Core.Service.Communication.Mapping;

public partial class MedinillaMapping
{
    public static RequestStartTransactionRequest MapRequestStartTransaction(
        Medinilla.Core.gRPC.Service.RequestStartTransactionRequest request)
    {
        return new RequestStartTransactionRequest
        {
            EvseId = request.HasEvseId ? request.EvseId : null,
            RemoteStartId = request.RemoteStartId,
        };
    }
}