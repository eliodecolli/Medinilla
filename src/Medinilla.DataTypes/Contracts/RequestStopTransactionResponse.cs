using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

public sealed class RequestStopTransactionResponse
{
    public RequestStartStopStatusEnum Status { get; set; }

    public StatusInfo? StatusInfo { get; set; }
}