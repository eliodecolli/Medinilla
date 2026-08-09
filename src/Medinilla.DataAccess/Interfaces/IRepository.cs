using System.Linq.Expressions;

namespace Medinilla.DataAccess.Interfaces;

public interface IRepository<T>
{
    Task<T?> Get(params object[] keyValues);

    Task<IEnumerable<T>> Filter(Expression<Func<T, bool>> predicate);

    Task<bool> DeleteOne(Expression<Func<T, bool>> predicate);

    Task<bool> DeleteMany(Expression<Func<T, bool>> predicate);

    Task<T> Create(T entity);

    Task<T> Update(T entity);
}
