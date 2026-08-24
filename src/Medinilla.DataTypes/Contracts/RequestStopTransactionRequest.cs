using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

public sealed class RequestStopTransactionRequest
{
    public string TransactionId { get; set; }
}