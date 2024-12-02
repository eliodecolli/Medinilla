using System.Text.Json;

namespace Medinilla.DataAccess.Relational.Models.Authorization;

public class AuthorizationDetails
{
    public Guid Id { get; set; }

    public Guid ChargingStationId { get; set; }

    public JsonDocument AuthBlob { get; set; }

    public virtual ChargingStation ChargingStation { get; set; }
}
