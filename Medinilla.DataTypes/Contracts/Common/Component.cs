using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

public sealed class Component
{
    [JsonConstructor]
    public Component(string name, string instance, EVSEType evse)
    {
        Name = name;
        Instance = instance;
        EVSE = evse;
    }

    public string Name { get; private set; }

    public string Instance { get; private set; }

    [JsonPropertyName("evse")]
    public EVSEType EVSE { get; private set; }
}
