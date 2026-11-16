namespace ProjectVital.Domain.Interfaces;

/// <summary>
/// Interface base para repositórios. Define operações CRUD genéricas.
/// Repositórios específicos podem estender esta interface com queries customizadas.
/// </summary>
/// <typeparam name="T">Tipo da entidade gerenciada.</typeparam>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
