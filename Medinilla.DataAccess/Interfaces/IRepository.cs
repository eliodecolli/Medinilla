namespace Medinilla.DataAccess.Interfaces;

public interface IRepository<T>
{
    Task<T?> Get(params object[] keyValues);

    Task<IEnumerable<T>> Filter(Func<T, bool> predicate);

    Task<bool> DeleteOne(Func<T, bool> predicate);

    Task<bool> DeleteMany(Func<T, bool> predicate);

    Task<T> Create(T entity);
}
