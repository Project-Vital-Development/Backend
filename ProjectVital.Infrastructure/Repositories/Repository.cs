using Microsoft.EntityFrameworkCore;
using ProjectVital.Domain.Interfaces;
using ProjectVital.Infrastructure.Persistence;

namespace ProjectVital.Infrastructure.Repositories;

/// <summary>
/// Implementação base de repositório com operações CRUD sobre o DbContext.
/// Repositórios concretos herdam desta e adicionam queries específicas.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly ProjectVitalDbContext Db;
    protected readonly DbSet<T> Set;

    public Repository(ProjectVitalDbContext db)
    {
        Db = db;
        Set = db.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await Set.FindAsync(new object[] { id }, cancellationToken);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().ToListAsync(cancellationToken);

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await Set.AddAsync(entity, cancellationToken);
        return entity;
    }

    public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        Set.Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        Set.Remove(entity);
        return Task.CompletedTask;
    }

    public virtual Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Db.SaveChangesAsync(cancellationToken);
}
