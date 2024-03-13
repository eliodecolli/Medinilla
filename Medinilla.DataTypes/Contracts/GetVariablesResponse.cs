using Medinilla.DataTypes.Contracts.Common;
using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts;

public sealed class GetVariablesResponse
{
    [JsonConstructor]
    public GetVariablesResponse(GetVariableResult getVariableResult)
    {
        GetVariableResult = getVariableResult;
    }

    public GetVariableResult GetVariableResult { get; private set; }
}
