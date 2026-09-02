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
            IdToken = MapIdToken(request.IdToken),
            GroupIdToken = request.GroupIdToken is not null ? MapIdToken(request.GroupIdToken) : null,
            RemoteStartId = request.RemoteStartId,
        };
    }

    private static IdToken MapIdToken(Medinilla.Core.gRPC.Contracts.IdToken grpc)
    {
        var a = grpc.AdditionalInfo;
        var hasAdditional = a is not null
            && (!string.IsNullOrEmpty(a.AdditionalIdToken) || !string.IsNullOrEmpty(a.Type));

        return new IdToken
        {
            Token = grpc.Token,
            Type = Enum.TryParse<IdTokenType>(grpc.Type, out var t) ? t : IdTokenType.NoAuthorization,
            AdditionalInfo = hasAdditional && a is not null
                ? new AdditionalTokenInfo
                {
                    AdditionalIdToken = a.AdditionalIdToken,
                    Type = a.Type,
                }
                : null,
        };
    }
}