using Microsoft.EntityFrameworkCore;
using ProjectVital.Domain.Interfaces;
using ProjectVital.Infrastructure.Persistence;

namespace ProjectVital.Infrastructure.Repositories;

/// <summary>
/// Implementação base genérica do padrão Repository.
/// Encapsula operações CRUD comuns usando EF Core, evitando duplicação nos repositórios concretos.
///
/// COMO USAR:
///   Repositórios concretos herdam desta classe e adicionam queries específicas.
///   Ex: StudentRepository : Repository{Student}, IStudentRepository
///
/// TRACKING vs NO-TRACKING:
///   - GetByIdAsync() usa Set.FindAsync() → RASTREADO pelo EF Core (pode ser modificado e salvo)
///   - GetAllAsync() usa AsNoTracking() → NÃO rastreado (mais leve para leituras puras)
///   - Repositórios concretos devem escolher conforme necessidade:
///     Use AsNoTracking() para listagens/reads; use rastreamento para update/delete
///
/// SAVECHANGES:
///   Não é chamado automaticamente em Add/Update/Delete — o chamador decide quando commitar.
///   Isso permite operações compostas (ex: Add User + Add Student → um único SaveChangesAsync).
///   SEMPRE chamar SaveChangesAsync() ao final de uma operação que modifica dados.
///
/// SE ADICIONAR UM NOVO MÉTODO BASE:
///   Adicionar também na interface IRepository{T} em Domain/Interfaces/IRepository.cs.
///
/// IMPACTO EM CASCATA: todos os repositórios concretos herdam este comportamento.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    /// <summary>
    /// DbContext compartilhado com todos os repositórios do mesmo request (Scoped).
    /// Protected para que repositórios concretos possam usá-lo em queries LINQ avançadas
    /// (ex: PredictionRepository usa Db.Predictions diretamente para o JOIN com subquery).
    /// </summary>
    protected readonly ProjectVitalDbContext Db;

    /// <summary>
    /// DbSet tipado da entidade T. Atalho para não chamar Db.Set{T}() toda vez.
    /// Todos os métodos deste repositório operam sobre este Set.
    /// </summary>
    protected readonly DbSet<T> Set;

    public Repository(ProjectVitalDbContext db)
    {
        Db = db;
        Set = db.Set<T>();
    }

    /// <summary>
    /// Busca por chave primária (int Id). Usa FindAsync() que primeiro verifica
    /// o cache do DbContext antes de ir ao banco — eficiente para operações sequenciais.
    ///
    /// TRACKING: rastreado pelo EF Core → entidade retornada pode ser modificada e salva.
    /// RETORNA NULL se não encontrado (verificar no chamador).
    ///
    /// SE A ENTIDADE TEM SOFT DELETE:
    ///   O Global Query Filter (DeletedAt IS NULL) é aplicado AUTOMATICAMENTE aqui também.
    ///   FindAsync() respeita query filters — entidades soft-deletadas retornam null.
    /// </summary>
    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await Set.FindAsync(new object[] { id }, cancellationToken);

    /// <summary>
    /// Lista todas as entidades do tipo T sem filtros.
    /// AsNoTracking() melhora performance em listagens grandes — não precisa de tracking
    /// quando os dados são apenas para leitura/exibição.
    ///
    /// ATENÇÃO: em tabelas grandes (CheckIns), evitar chamar este método sem filtros.
    /// Prefira métodos específicos nos repositórios concretos (ex: GetByStudentAsync).
    /// </summary>
    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().ToListAsync(cancellationToken);

    /// <summary>
    /// Adiciona a entidade ao change tracker do EF Core (não vai ao banco ainda).
    /// O INSERT real ocorre somente quando SaveChangesAsync() for chamado.
    ///
    /// PARA INSERÇÃO ATÔMICA:
    ///   Chame AddAsync() para múltiplas entidades → chame SaveChangesAsync() uma vez.
    ///   Todas as inserções ocorrem na mesma transação.
    /// </summary>
    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await Set.AddAsync(entity, cancellationToken);
        return entity;
    }

    /// <summary>
    /// Marca a entidade como modificada no change tracker (não vai ao banco ainda).
    /// O UPDATE real ocorre somente quando SaveChangesAsync() for chamado.
    ///
    /// ATENÇÃO: Set.Update() marca TODOS os campos como modificados → gera UPDATE com todas as colunas.
    /// Em cenários de alta concorrência, prefer UpdateAsync() com entidade rastreada + modificação direta.
    /// </summary>
    public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        Set.Update(entity);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Marca a entidade para DELETE (não vai ao banco ainda).
    /// O DELETE real ocorre somente quando SaveChangesAsync() for chamado.
    ///
    /// ATENÇÃO: Este é DELETE FÍSICO. Para entidades com soft delete (User, Student),
    /// use SoftDelete() + UpdateAsync() em vez deste método.
    /// </summary>
    public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        Set.Remove(entity);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Envia todas as alterações pendentes no change tracker para o banco em uma transação.
    /// Retorna o número de linhas afetadas.
    ///
    /// QUANDO CHAMAR: sempre após Add/Update/Delete. Nunca chamar mais de uma vez
    /// para o mesmo conjunto de alterações — o segundo SaveChanges não faria nada.
    /// </summary>
    public virtual Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Db.SaveChangesAsync(cancellationToken);
}
