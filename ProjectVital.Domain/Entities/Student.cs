namespace ProjectVital.Domain.Entities;

/// <summary>
/// Representa um aluno matriculado em uma academia.
/// É a entidade central do sistema — tudo orbita em torno do Student:
///   - CheckIns registram frequência → alimentam o modelo ML
///   - Predictions armazenam o resultado do modelo ML
///
/// MULTI-TENANT: cada Student pertence a uma única Academy (AcademyId).
/// Os resolvers GraphQL usam ResolverAuthHelper.EnforceAcademyAccess() para
/// garantir que um Manager só acessa Students da sua própria academia.
///
/// RELAÇÕES:
///   - Student (N) → (1) User     : conta de acesso do aluno
///   - Student (N) → (1) Academy  : academia à qual o aluno pertence
///   - Student (N) → (1) Plan     : plano de mensalidade atual
///   - Student (1) → (N) CheckIn  : histórico de entradas na academia
///   - Student (1) → (N) Prediction : histórico de predições de churn
///
/// IMPACTO EM CASCATA: se você alterar campos aqui, também precisa atualizar:
///   - StudentConfiguration.cs       → mapeamento EF Core
///   - StudentDto.cs                 → o que o frontend recebe
///   - StudentRepository.cs          → queries (especialmente os .Include())
///   - StudentService.cs             → lógica de registro, update, soft delete
///   - ML queries.py                 → as queries SQL que leem Students para o modelo
///   - Migration nova                → toda mudança de schema exige nova migration
/// </summary>
public class Student
{
    public int Id { get; set; }

    /// <summary>
    /// FK para User (relação 1:1). O User tem Role=Student.
    /// Criados atomicamente em StudentService.RegisterAsync() — User e Student
    /// são inseridos no mesmo SaveChanges usando navegação EF Core.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// FK para Academy. Define o tenant ao qual o aluno pertence.
    /// CRÍTICO para multi-tenancy: todas as queries de aluno filtram por AcademyId.
    /// Se mudar este campo, atualizar ResolverAuthHelper.EnforceAcademyAccess().
    /// </summary>
    public int AcademyId { get; set; }

    /// <summary>
    /// FK para Plan. Pode ser alterado via StudentService.UpdateAsync().
    /// Não afeta o histórico de check-ins — apenas indica o plano atual.
    /// </summary>
    public int PlanId { get; set; }

    /// <summary>Quando o aluno se matriculou. Setado em RegisterAsync, padrão = agora.</summary>
    public DateTime EnrollmentDate { get; set; }

    /// <summary>
    /// Indica se o aluno está ativo.
    /// false = aluno desativado (não pode fazer check-in, mas permanece no banco).
    /// No modelo ML (queries.py), Active=FALSE é tratado como label churn=1.
    /// Se mudar esta lógica, atualizar load_all_checkins_for_training() em queries.py.
    /// </summary>
    public bool Active { get; set; } = true;

    /// <summary>
    /// LGPD Art.18 — soft delete. Null = ativo; preenchido = excluído logicamente.
    /// O Global Query Filter em ProjectVitalDbContext garante invisibilidade automática.
    /// Chamado por StudentService.DeleteAsync() junto com User.SoftDelete().
    /// As queries do ML Service (queries.py) também filtram DeletedAt IS NULL.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Marca o aluno como excluído logicamente.
    /// Deve ser chamado JUNTO com User.SoftDelete() para manter consistência.
    /// Veja StudentService.DeleteAsync() para o fluxo completo.
    /// </summary>
    public void SoftDelete() => DeletedAt = DateTime.UtcNow;

    // ── Navigation Properties (não são colunas — carregadas via Include) ─────────

    /// <summary>Dados do usuário (nome, email). Carregado via .Include(s => s.User).</summary>
    public User User { get; set; } = null!;

    /// <summary>Academia do aluno. Carregado via .Include(s => s.Academy).</summary>
    public Academy Academy { get; set; } = null!;

    /// <summary>Plano atual. Carregado via .Include(s => s.Plan).</summary>
    public Plan Plan { get; set; } = null!;

    /// <summary>
    /// Histórico completo de check-ins. NÃO carregado por padrão — use
    /// CheckInRepository.GetByStudentAsync() para queries filtradas.
    /// Esta coleção alimenta o modelo ML via SQL direto no Python.
    /// </summary>
    public ICollection<CheckIn> CheckIns { get; set; } = new List<CheckIn>();

    /// <summary>
    /// Histórico completo de predições. NÃO carregado por padrão.
    /// Para dashboard de risco, use PredictionRepository.GetLatestByAcademyAsync()
    /// que retorna apenas a predição mais recente por aluno (evita N+1).
    /// </summary>
    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
}
