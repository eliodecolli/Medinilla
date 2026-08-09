using System.Linq.Expressions;
using Medinilla.DataAccess.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Medinilla.DataAccess.Relational
{
    public sealed class GenericRepository<T>(MedinillaOcppDbContext context) : IRepository<T>
        where T : class
    {
        public async Task<T> Create(T entity)
        {
            var result = await context.Set<T>().AddAsync(entity);
            return result.Entity;
        }

        public async Task<bool> DeleteMany(Expression<Func<T, bool>> predicate)
        {
            await context.Set<T>().Where(predicate).ExecuteDeleteAsync();
            return true;
        }

        public async Task<bool> DeleteOne(Expression<Func<T, bool>> predicate)
        {
            var item = await context.Set<T>().FirstOrDefaultAsync(predicate);
            if (item is null)
            {
                return false;
            }

            context.Set<T>().Remove(item);
            return true;
        }

        public async Task<IEnumerable<T>> Filter(Expression<Func<T, bool>> predicate)
        {
            return await Task.FromResult<IEnumerable<T>>(context.Set<T>().Where(predicate));
        }

        public async Task<T?> Get(params object[] keyValues)
        {
            return await context.Set<T>().FindAsync(keyValues);
        }

        public async Task<T> Update(T entity)
        {
            var result = context.Set<T>().Update(entity);
            return result.Entity;
        }
    }
}
