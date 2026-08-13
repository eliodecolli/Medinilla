using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

public class ResetRequest
{
    public ResetEnum Type { get; set; }

    public int? EvseId { get; set; }
}
