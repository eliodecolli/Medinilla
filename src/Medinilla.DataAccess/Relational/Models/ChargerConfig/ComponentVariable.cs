using Medinilla.DataAccess.Relational.Enums;

namespace Medinilla.DataAccess.Relational.Models.ChargerConfig;

public class ComponentVariable
{
    public Guid Id { get; set; }

    public long ChargerComponentId { get; set; }

    public required string Name {  get; set; }
    
    public string? Instance { get; set; }
    
    public string? Value { get; set; }
    
    public bool? Constant { get; set; }
    
    public VariableAttributeType  AttributeType { get; set; }

    public VariableMutability  Mutability { get; set; }

    public string? Unit { get; set; }

    public string? DataType { get; set; }

    public decimal? MinLimit { get; set; }

    public decimal? MaxLimit { get; set; }

    public string? ValuesList  { get; set; }

    public virtual ChargerComponent Component { get; set; }
}