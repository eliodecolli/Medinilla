using Medinilla.DataAccess.Relational.Models;
using Microsoft.EntityFrameworkCore;

namespace Medinilla.DataAccess.Relational;

public class MedinillaOcppDbContext : DbContext
{
    public DbSet<Transaction> TransactionSet { get; set; }
}
