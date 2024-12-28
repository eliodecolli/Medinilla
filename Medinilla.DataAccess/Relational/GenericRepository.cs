using Medinilla.DataAccess.Interfaces;

namespace Medinilla.DataAccess.Relational
{
    public sealed class GenericRepository<T>(MedinillaOcppDbContext context) : IRepository<T>
        where T : class, new()
    {
        public async Task<T> Create(T entity)
        {
            var result = await context.Set<T>().AddAsync(entity);
            return result.Entity;
        }

        public async Task<bool> DeleteMany(Func<T, bool> predicate)
        {
            var items = context.Set<T>().Where(predicate);
            if (!items.Any())
            {
                return await Task.FromResult(false);
            }

            context.Set<T>().RemoveRange(items);

            return await Task.FromResult(true);
        }

        public async Task<bool> DeleteOne(Func<T, bool> predicate)
        {
            var item = context.Set<T>().First(predicate);
            if (item is null)
            {
                return await Task.FromResult(false);
            }

            context.Set<T>().Remove(item);
            return await Task.FromResult(true);
        }

        public async Task<IEnumerable<T>> Filter(Func<T, bool> predicate)
        {
            return await Task.FromResult(context.Set<T>().Where(predicate));
        }

        public async Task<T?> Get(params object[] keyValues)
        {
            return await context.Set<T>().FindAsync(keyValues);
        }
    }
}
