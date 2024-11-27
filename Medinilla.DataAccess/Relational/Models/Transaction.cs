using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Medinilla.DataAccess.Relational.Models;

[Table("transactions_event")]
[PrimaryKey("TransactionId", "SeqNo")]
public sealed class Transaction
{
    public string TransactionId { get; set; }

    public int SeqNo { get; set; }

    public int? EVSEId { get; set; }

    public DateTime Timestamp { get; set; }

    public string? IdToken { get; set; }

    public bool Offline { get; set; }
}
