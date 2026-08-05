using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.Core.Service.Communication.Mapping;

public partial class MedinillaMapping
{
    public static SetVariablesRequest MapSetVariables(Medinilla.Core.gRPC.Service.SetVariablesRequest request)
    {
        return new SetVariablesRequest
        {
            SetVariableData = request.SetVariableData
                .Select(d => new SetVariableData
                {
                    Component = new Component
                    {
                        Name = d.ComponentName,
                        Instance = d.ComponentInstance,
                    },
                    Variable = new Variable
                    {
                        Name = d.VariableName,
                        Instance = d.VariableInstance,
                    },
                    AttributeType = Enum.TryParse<AttributeEnum>(d.AttributeType, out var attr) ? attr : null,
                    AttributeValue = d.AttributeValue,
                })
                .ToList(),
        };
    }
}
