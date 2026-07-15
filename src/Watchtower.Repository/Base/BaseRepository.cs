using Microsoft.EntityFrameworkCore;
using Watchtower.Entities;

namespace Watchtower.Repository.Base;

public interface IBaseRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id);
    Task<List<T>?> GetAll();
    Task<T> AddAsync(T entity);
    Task AddRangeAsync(List<T> entities);
    Task RemoveAsync(Guid id);
    Task RemoveRangeAsync(IEnumerable<T> entities);
    Task UpdateAsync(T entity);
    Task UpdateRangeAsync(List<T> entities);
    void Attach(T entity);
    Task<int> GetCountAsync();
}

public abstract class BaseRepository<T, TDbContext> : IBaseRepository<T>
    where T : BaseEntity
    where TDbContext : DbContext
{
    protected readonly TDbContext db;

    protected BaseRepository(TDbContext db)
    {
        this.db = db;
    }

    public void Attach(T entity) => db.Attach(entity);

    public virtual async Task<T> AddAsync(T entity)
    {
        entity.Id = Guid.NewGuid();
        db.Set<T>().Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public async Task AddRangeAsync(List<T> entities)
    {
        entities.ForEach(e => e.Id = Guid.NewGuid());
        await db.Set<T>().AddRangeAsync(entities);
        await db.SaveChangesAsync();
    }

    public async Task<List<T>?> GetAll() => await db.Set<T>().ToListAsync();

    public virtual async Task<T?> GetByIdAsync(Guid id) => await db.Set<T>().FindAsync(id);

    public virtual async Task RemoveAsync(Guid id)
    {
        var entity = await db.Set<T>().FindAsync(id);
        if (entity is not null)
        {
            db.Set<T>().Remove(entity);
            await db.SaveChangesAsync();
        }
    }

    public virtual async Task RemoveRangeAsync(IEnumerable<T> entities)
    {
        db.Set<T>().RemoveRange(entities);
        await db.SaveChangesAsync();
    }

    public virtual async Task UpdateAsync(T entity)
    {
        db.Set<T>().Update(entity);
        await db.SaveChangesAsync();
    }

    public async Task UpdateRangeAsync(List<T> entities)
    {
        db.Set<T>().UpdateRange(entities);
        await db.SaveChangesAsync();
    }

    public async Task<int> GetCountAsync() => await db.Set<T>().CountAsync();
}
