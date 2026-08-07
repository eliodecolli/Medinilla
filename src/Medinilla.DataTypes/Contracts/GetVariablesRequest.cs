using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

public class GetVariablesRequest
{
    public List<GetVariableData> GetVariableData { get; set; }
}
