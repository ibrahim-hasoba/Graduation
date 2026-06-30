using Graduation.DAL.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace Graduation.DAL.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<T> Repository<T>() where T : class;
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action);
        Task ExecuteInTransactionAsync(Func<Task> action);
        Task<int> ExecuteSqlInterpolatedAsync(FormattableString sql, CancellationToken cancellationToken = default);
        Task ReloadAsync<T>(T entity) where T : class;
        DatabaseContext Context { get; }
    }
}
