namespace Medinilla.DataTypes.Contracts.Common;

public class ReportData
{
    public Component Component { get; set; }

    public Variable Variable { get; set; }

    public List<VariableAttribute>? VariableAttribute { get; set; }

    public VariableCharacteristics? VariableCharacteristics { get; set; }
}
