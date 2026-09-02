using Medinilla.Core.gRPC.Contracts;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.Core.Service.Communication.Mapping;

public partial class MedinillaMapping
{
    public static ResetRequest MapReset(Medinilla.Core.gRPC.Service.ResetRequest request)
    {
        return new ResetRequest
        {
            Type = request.Type switch
            {
                ResetType.Immediate => ResetEnum.Immediate,
                ResetType.OnIdle => ResetEnum.OnIdle,
                _ => ResetEnum.Immediate,
            },
            EvseId = request.HasEvseId ? request.EvseId : null,
        };
    }
}
