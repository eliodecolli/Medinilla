namespace Medinilla.DataAccess.Relational.UnitOfWork;

public abstract class BaseUnitOfWork(MedinillaOcppDbContext context)
{
    protected bool _disposed = false;

    public async Task Save()
    {
        if (_disposed)
        {
            return;
        }

        await context.SaveChangesAsync();
    }

    public async Task Discard()
    {
        if (_disposed)
        {
            return;
        }

        await context.DisposeAsync();
        _disposed = true;
    }
}
