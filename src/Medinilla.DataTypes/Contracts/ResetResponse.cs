using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

public class ResetResponse
{
    public ResetStatusEnum Status { get; set; }

    public StatusInfo StatusInfo { get; set; }
}
