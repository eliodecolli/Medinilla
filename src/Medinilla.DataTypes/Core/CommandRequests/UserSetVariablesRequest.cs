using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Core.CommandRequests;

public sealed class UserSetVariablesRequest : BaseCommandRequest
{
    public string ComponentName { get; set; }

    public string? ComponentInstance { get; set; }

    public string VariableName { get; set; }

    public string? VariableInstance { get; set; }

    public AttributeEnum? AttributeType { get; set; }

    public string AttributeValue { get; set; }
}