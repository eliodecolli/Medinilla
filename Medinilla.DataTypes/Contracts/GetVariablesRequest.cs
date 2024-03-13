using Medinilla.DataTypes.Contracts.Common;
using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts;

public sealed class GetVariablesRequest
{
    [JsonConstructor]
    public GetVariablesRequest(GetVariableData getVariableData)
    {
        GetVariableData = getVariableData;
    }

    public GetVariableData GetVariableData { get; private set; }
}
