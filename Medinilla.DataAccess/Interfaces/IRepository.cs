namespace Medinilla.DataAccess.Interfaces;

public interface IRepository<T>
{
    Task<T> Get(Predicate<T> predicate);

    Task<IQueryable<T>> Filter(Predicate<T> predicate);

    Task<bool> DeleteOne(Predicate<T> predicate);

    Task<bool> DeleteMany(Predicate<T> predicate);

    Task<T> Create(T entity);
}
