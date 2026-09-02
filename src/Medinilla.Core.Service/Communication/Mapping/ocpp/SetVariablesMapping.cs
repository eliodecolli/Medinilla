using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.Core.Service.Communication.Mapping;

public partial class MedinillaMapping
{
    public static SetVariablesRequest MapSetVariables(Medinilla.Core.gRPC.Service.SetVariablesRequest request)
    {
        return new SetVariablesRequest
        {
            SetVariableData = request.SetVariableData?
                .Where(d => d is not null)
                .Select(d => new SetVariableData
                {
                    Component = new Component
                    {
                        Name = d.ComponentName ?? string.Empty,
                        Instance = string.IsNullOrEmpty(d.ComponentInstance) ? null : d.ComponentInstance,
                    },
                    Variable = new Variable
                    {
                        Name = d.VariableName ?? string.Empty,
                        Instance = string.IsNullOrEmpty(d.VariableInstance) ? null : d.VariableInstance,
                    },
                    AttributeType = Enum.TryParse<AttributeEnum>(d.AttributeType, out var attr) ? attr : null,
                    AttributeValue = d.AttributeValue,
                })
                .ToList() ?? [],
        };
    }
}
