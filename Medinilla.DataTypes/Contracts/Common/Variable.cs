using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

public sealed class Variable
{
    [JsonConstructor]
    public Variable(string name, string instance)
    {
        Name = name;
        Instance = instance;
    }

    public string Name { get; private set; }

    public string Instance { get; private set; }
}
