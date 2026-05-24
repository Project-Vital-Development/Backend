using Microsoft.EntityFrameworkCore;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Interfaces;
using ProjectVital.Infrastructure.Persistence;

namespace ProjectVital.Infrastructure.Repositories;

/// <summary>
/// Repositório de usuários — acessa a tabela Users no MySQL.
/// Herda operações CRUD base de Repository&lt;User&gt;.
///
/// REGISTRADO EM: Program.cs → services.AddScoped&lt;IUserRepository, UserRepository&gt;()
///
/// CONSUMERS:
///   - AuthService.LoginAsync() → usa GetByEmailAsync para buscar usuário por email
///   - StudentService.RegisterAsync() → usa EmailExistsAsync para verificar unicidade
///   - StudentService.DeleteAsync() → usa UpdateAsync (herdado) para soft delete
///
/// SOFT DELETE:
///   Global Query Filter em ProjectVitalDbContext filtra automaticamente DeletedAt IS NULL.
///   Todos os métodos herdados e os desta classe NÃO retornam usuários deletados.
///   Para incluir deletados: usar IgnoreQueryFilters() (não exposto nesta classe).
/// </summary>
public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(ProjectVitalDbContext db) : base(db) { }

    /// <summary>
    /// Busca usuário por email (case-sensitive no MySQL com collation padrão).
    /// Retorna null se não existir ou estiver soft-deleted.
    ///
    /// NÃO USA AsNoTracking:
    ///   O EF Core rastreia o objeto retornado — necessário se o caller precisar
    ///   atualizar o usuário na mesma operação (ex: mudar senha, atualizar dados).
    ///   Para leitura pura, AsNoTracking seria mais performático, mas a consistência
    ///   de tracking com o restante do contexto vale o custo.
    ///
    /// USADO POR: AuthService.LoginAsync() — busca o usuário para verificar a senha.
    /// </summary>
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // Não usar AsNoTracking aqui — o caller pode querer atualizar (mudança de senha, etc.)
        return await Set.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    /// <summary>
    /// Verifica se um email já está cadastrado (sem carregar o objeto completo).
    /// Mais eficiente que GetByEmailAsync quando apenas a existência interessa.
    ///
    /// AnyAsync gera: SELECT 1 FROM Users WHERE Email = @email LIMIT 1
    /// — mais rápido que SELECT * porque não carrega todas as colunas.
    ///
    /// USADO POR: StudentService.RegisterAsync() — validação de email único antes de criar.
    /// </summary>
    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await Set.AnyAsync(u => u.Email == email, cancellationToken);
    }
}
