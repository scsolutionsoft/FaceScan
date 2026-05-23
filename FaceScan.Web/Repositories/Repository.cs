using System.Linq.Expressions;
using FaceScan.Web.Data;
using FaceScan.Web.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly ApplicationDbContext DbContext;
    protected readonly DbSet<T> DbSet;

    public Repository(ApplicationDbContext dbContext)
    {
        DbContext = dbContext;
        DbSet = dbContext.Set<T>();
    }

    public IQueryable<T> Query() => DbSet.AsQueryable();

    public Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return DbSet.FindAsync([id], cancellationToken).AsTask();
    }

    public Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        return DbSet.AddAsync(entity, cancellationToken).AsTask();
    }

    public void Update(T entity)
    {
        DbSet.Update(entity);
    }

    public void Remove(T entity)
    {
        DbSet.Remove(entity);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return DbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return DbSet.AnyAsync(predicate, cancellationToken);
    }
}
