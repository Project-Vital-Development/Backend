using ProjectVital.Domain.Enums;

namespace ProjectVital.Domain.Entities;

/// <summary>
/// Conta de acesso ao sistema. Qualquer pessoa que faz login é um User —
/// tanto gestores de academia (Manager) quanto alunos (Student).
///
/// RELAÇÕES:
///   - User (1) → (0..1) Academy : um Manager gerencia exatamente uma academia
///   - User (1) → (0..1) Student : um usuário com Role=Student tem um Student vinculado
///
/// IMPACTO EM CASCATA: se você alterar campos aqui, também precisa atualizar:
///   - UserConfiguration.cs        → mapeamento EF Core (tamanho de colunas, índices)
///   - UserDto.cs                  → DTO de saída da API (o que o frontend recebe)
///   - StudentService.RegisterAsync → cria User + Student juntos atomicamente
///   - AuthService.LoginAsync       → lê Email e PasswordHash para autenticar
///   - Migration nova              → toda mudança de schema exige nova migration EF Core
/// </summary>
public class User
{
    public int Id { get; set; }

    /// <summary>Nome completo exibido na UI. Mapeado como VARCHAR(255) no MySQL.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Email usado como login. Deve ser único no sistema (índice UNIQUE no MySQL).
    /// Se duplicar → BusinessRuleException lançada em StudentService.RegisterAsync.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Hash BCrypt da senha (work factor 12). NUNCA armazenar senha em texto plano.
    /// Gerado por AuthService.HashPassword(). Verificado por AuthService.VerifyPassword().
    /// Se mudar o work factor, usuários antigos ainda autenticam — BCrypt auto-detecta o factor.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Papel do usuário no sistema. Define o que ele pode acessar via JWT.
    /// O claim "role" no token JWT é derivado deste campo por AuthService.GenerateToken().
    /// Se mudar os valores do enum UserRole, precisa atualizar:
    ///   - ResolverAuthHelper.EnforceAcademyAccess (compara string "Admin")
    ///   - AuthService.LoginAsync (compara UserRole.Manager para incluir academyId no token)
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>Timestamp UTC de criação. Setado uma vez, nunca atualizado.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// LGPD Art.18 — direito de exclusão via soft delete.
    /// Null = usuário ativo (visível em todas as queries normais).
    /// Preenchido = usuário excluído logicamente (filtrado automaticamente pelo
    /// Global Query Filter em ProjectVitalDbContext.OnModelCreating).
    ///
    /// IMPORTANTE: Use .IgnoreQueryFilters() no EF Core se precisar acessar usuários excluídos.
    /// NUNCA fazer DELETE físico — viola LGPD e perde histórico de predições.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Marca o usuário como excluído logicamente (LGPD).
    /// Chamado por StudentService.DeleteAsync junto com Student.SoftDelete().
    /// Após chamar, o usuário desaparece de todas as queries (Global Query Filter).
    /// </summary>
    public void SoftDelete() => DeletedAt = DateTime.UtcNow;

    // ── Navigation Properties (carregadas via Include no EF Core) ────────────────
    // Estas propriedades NÃO são colunas no banco — são relações carregadas por EF Core.
    // Para acessá-las, use .Include(u => u.ManagedAcademy) na query.

    /// <summary>
    /// Academia que este usuário gerencia. Null se Role != Manager.
    /// Carregada via AcademyRepository.GetByManagerIdAsync().
    /// </summary>
    public Academy? ManagedAcademy { get; set; }

    /// <summary>
    /// Registro de aluno vinculado. Null se Role != Student.
    /// Carregada via StudentRepository.GetWithRelationsAsync().
    /// </summary>
    public Student? Student { get; set; }
}
