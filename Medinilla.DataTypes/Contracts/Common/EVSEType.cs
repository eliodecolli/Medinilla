using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

public sealed class EVSEType
{
    [JsonConstructor]
    public EVSEType(int id, int connectorId)
    {
        Id = id; 
        ConnectorId = connectorId;
    }

    public int Id { get; private set; }

    public int ConnectorId { get; private set; }
}
