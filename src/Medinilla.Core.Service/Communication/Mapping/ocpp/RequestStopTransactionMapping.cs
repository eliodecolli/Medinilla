using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.Core.Service.Communication.Mapping;

public partial class MedinillaMapping
{
    public static RequestStopTransactionRequest MapRequestStopTransaction(
        Medinilla.Core.gRPC.Service.RequestStopTransactionRequest request)
    {
        return new RequestStopTransactionRequest
        {
            TransactionId = request.TransactionId,
        };
    }
}