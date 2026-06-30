using System.Linq.Expressions;
using Graduation.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace Graduation.DAL.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly DatabaseContext Context;
        protected readonly DbSet<T> DbSet;

        public Repository(DatabaseContext context)
        {
            Context = context;
            DbSet = context.Set<T>();
        }

        public IQueryable<T> Query() => DbSet;

        public ValueTask<T?> GetByIdAsync(params object[] id) => DbSet.FindAsync(id);

        public Task<List<T>> GetAllAsync() => DbSet.ToListAsync();

        public Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate)
            => DbSet.Where(predicate).ToListAsync();

        public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
            => DbSet.FirstOrDefaultAsync(predicate);

        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
            => DbSet.AnyAsync(predicate);

        public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
            => predicate == null ? DbSet.CountAsync() : DbSet.CountAsync(predicate);

        public void Add(T entity) => DbSet.Add(entity);

        public void AddRange(IEnumerable<T> entities) => DbSet.AddRange(entities);

        public void Update(T entity) => DbSet.Update(entity);

        public void Delete(T entity) => DbSet.Remove(entity);

        public void DeleteRange(IEnumerable<T> entities) => DbSet.RemoveRange(entities);
    }
}
