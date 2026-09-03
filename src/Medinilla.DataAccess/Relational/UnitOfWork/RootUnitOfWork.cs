using System.Linq.Expressions;
using Medinilla.DataAccess.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Medinilla.DataAccess.Relational.UnitOfWork;

public abstract class RootUnitOfWork<T>(MedinillaOcppDbContext context) : BaseUnitOfWork(context)
    where T : class
{
    private T _root;
    private bool _started = false;
    private readonly MedinillaOcppDbContext _context = context;

    private void AssertStarted()
    {
        if (!_started) throw new InvalidOperationException("UnitOfWork not started.");
    }

    protected DbSet<TEntity> GetDbSet<TEntity>() where TEntity : class
    {
        AssertStarted();
        return context.Set<TEntity>();
    }

    public T AggregateRoot => _root;

    public async Task Start(Expression<Func<T, bool>> predicate)
    {
        if (_started) return;
        
        _root = await _context.Set<T>().Where(predicate).FirstOrDefaultAsync() ?? throw new AggregateRootNotFoundException($"Aggregate root of type {typeof(T).Name} not found.");
        _started = true;
    }

    public async Task Start(T entity)
    {
        // leave the started check out of this for now
        _root = (await _context.Set<T>().AddAsync(entity).ConfigureAwait(false)).Entity;
        _started = true;
    }

    public IQueryable<T> FetchAll()
    {
        return _context.Set<T>().AsQueryable();
    }

    public async Task TrackAddedEntity<TEntity>(TEntity entity) where TEntity : class
    {
        await _context.Set<TEntity>().AddAsync(entity);
    }
}