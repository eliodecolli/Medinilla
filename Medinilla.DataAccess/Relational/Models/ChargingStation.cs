using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Medinilla.DataAccess.Relational.Models;

[Table("charging_station")]
[PrimaryKey("Id")]
public sealed class ChargingStation
{
    public string Id { get; set; }

    public string Model {  get; set; }

    public string Vendor { get; set; }

    public string LatestBootNotificationReason {  get; set; }

    public DateTime CreatedAt {  get; set; }

    public DateTime? ModifiedAt { get; set; }
}
