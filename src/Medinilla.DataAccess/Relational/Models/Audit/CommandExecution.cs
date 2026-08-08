using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Medinilla.DataAccess.Relational.Models.Audit;

public class CommandExecution
{
    public long Id { get; set; }

    public required string ChargingStationClientIdentifier { get; set; }

    public required string ActionName { get; set; }

    public required string MessageId { get; set; }

    public required DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public required bool Completed { get; set; }

    public required bool Error { get; set; }

    public string? ErrorMessage { get; set; }
}
