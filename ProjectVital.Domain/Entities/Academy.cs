namespace ProjectVital.Domain.Entities;

/// <summary>
/// Academia cadastrada na plataforma — é o TENANT ROOT do sistema multitenant.
/// Cada cliente SaaS é representado por uma Academy. Todos os dados do sistema
/// (alunos, planos, check-ins, predições) são isolados por Academy.
///
/// MULTITENANT — O QUE ISSO SIGNIFICA:
///   - Uma única instância da API serve N academias simultaneamente
///   - Um Manager da Academia A NUNCA deve ver dados da Academia B
///   - O isolamento é garantido por duas camadas:
///     1. JWT: o claim "academyId" limita o Manager à sua academia
///     2. Código: ResolverAuthHelper.EnforceAcademyAccess() verifica em cada resolver
///   - Se o isolamento falhar → um gestor pode ver alunos e predições de concorrentes
///
/// RELAÇÕES:
///   - Academy (1) → (1) User    : um Manager (User.Role=Manager) por academia
///   - Academy (1) → (N) Plan    : planos de mensalidade da academia
///   - Academy (1) → (N) Student : alunos matriculados
///
/// IMPACTO EM CASCATA: se você alterar campos aqui, também precisa atualizar:
///   - AcademyConfiguration.cs      → mapeamento EF Core (tamanhos, FKs)
///   - AcademyDto.cs                → DTO de saída para a API
///   - AcademyRepository.cs         → queries específicas (GetByManagerIdAsync, GetWithPlansAsync)
///   - AuthService.LoginAsync()     → busca Academy por ManagerId para incluir no token
///   - Program.cs SeedData          → seed de academias de teste
///   - Migration nova               → toda mudança de schema exige nova migration EF Core
/// </summary>
public class Academy
{
    public int Id { get; set; }

    /// <summary>
    /// Nome comercial da academia. Exibido no dashboard e nos relatórios.
    /// Máximo 255 chars (configurado em AcademyConfiguration.cs).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Endereço completo (opcional). Null = não informado.
    /// O frontend deve exibir "Não informado" quando null.
    /// Máximo 500 chars.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// FK para o User com Role=Manager que administra esta academia.
    /// Modelo atual: 1:1 — um Manager por Academia.
    /// Este Id é incluído no JWT do Manager como claim "academyId" após login.
    /// SE NO FUTURO UM MANAGER GERENCIAR MÚLTIPLAS ACADEMIAS:
    ///   Remover esta FK e criar tabela de junção Academy_Managers.
    ///   Atualizar AcademyRepository.GetByManagerIdAsync() (hoje retorna nullable).
    ///   Atualizar a lógica de JWT (claim "academyId" vira lista ou seleção).
    /// </summary>
    public int ManagerId { get; set; }

    /// <summary>
    /// Quando a academia foi cadastrada na plataforma (UTC).
    /// DEFAULT CURRENT_TIMESTAMP(6) no MySQL — setado automaticamente no INSERT.
    /// Nunca atualizado após a criação.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation Properties (não são colunas — carregadas via Include) ─────────

    /// <summary>
    /// User (Manager) que administra a academia.
    /// Carregado via AcademyConfiguration.HasOne(a => a.Manager).
    /// Null se não foi carregado com .Include().
    /// </summary>
    public User Manager { get; set; } = null!;

    /// <summary>
    /// Planos de mensalidade oferecidos pela academia (ex: Mensal, Trimestral, Anual).
    /// Carregados via .Include(a => a.Plans) em AcademyRepository.GetWithPlansAsync().
    /// Usado por StudentService.RegisterAsync() para validar que o PlanId do input
    /// pertence à academia do aluno (tenant isolation de planos).
    /// </summary>
    public ICollection<Plan> Plans { get; set; } = new List<Plan>();

    /// <summary>
    /// Todos os alunos matriculados nesta academia.
    /// NÃO carregado por padrão — use StudentRepository.GetByAcademyIdAsync().
    /// Esta coleção é potencialmente grande — evitar carregamento em memória sem filtros.
    /// </summary>
    public ICollection<Student> Students { get; set; } = new List<Student>();
}
