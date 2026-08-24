using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

public sealed class RequestStartTransactionResponse
{
    public RequestStartStopStatusEnum Status { get; set; }

    public StatusInfo? StatusInfo { get; set; }

    public string? TransactionId { get; set; }
}