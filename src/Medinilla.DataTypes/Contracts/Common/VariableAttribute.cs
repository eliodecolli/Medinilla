namespace Medinilla.DataTypes.Contracts.Common;

public class VariableAttribute
{
    public AttributeEnum? Type { get; set; }

    public string? Value { get; set; }

    public MutabilityEnum? Mutability { get; set; }

    public bool? Persistent { get; set; }

    public bool? Constant { get; set; }
}
