namespace Medinilla.App.WebApi.Models;

public class ChargingStationApiModel
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public string? Alias { get; set; }

    public string? Location { get; set; }

    public string LastStatus { get; set; }
}
