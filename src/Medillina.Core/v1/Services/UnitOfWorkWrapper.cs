using Medinilla.Core.Interfaces.Services;
using Medinilla.DataAccess.Relational;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Medinilla.Core.v1.Services;

/// <inheritdoc/>
public class UnitOfWorkWrapper(MedinillaOcppDbContext context) : IUnitOfWorkWrapper
{
    public async Task SaveChanges()
    {
        await context.SaveChangesAsync();
    }

    public void DiscardChanges()
    {
        context.ChangeTracker.Clear();
    }
}