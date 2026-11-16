namespace ProjectVital.Domain.Entities;

/// <summary>
/// Plano de mensalidade oferecido por uma academia (ex: Mensal, Trimestral, Anual).
/// Cada plano pertence exclusivamente a uma Academy — é multitenant por design.
///
/// MULTITENANT:
///   Um Manager da Academia A não deve ver ou atribuir planos da Academia B.
///   O isolamento é garantido em duas camadas:
///   1. Banco: FK Plans.AcademyId → Academies.Id
///   2. Código: StudentService.RegisterAsync() valida via AcademyRepository.GetWithPlansAsync()
///      que o PlanId fornecido no input pertence à academia do aluno.
///
/// RELAÇÕES:
///   - Plan (N) → (1) Academy   : plano pertence a uma academia
///   - Plan (1) → (N) Student   : alunos matriculados neste plano
///
/// CICLO DE VIDA:
///   Planos são criados por Admins via seed ou futura interface de CRUD.
///   Planos com alunos vinculados NÃO podem ser deletados (Restrict FK em StudentConfiguration).
///   Para descontinuar um plano: desativar via campo IsActive (não existe ainda — TD futura).
///
/// IMPACTO EM CASCATA: se você alterar campos aqui, também precisa atualizar:
///   - PlanConfiguration.cs      → mapeamento EF Core (precisão do decimal, FK)
///   - PlanDto.cs                → DTO de saída para a API
///   - PlanRepository.cs         → GetByAcademyIdAsync (ordena por Price)
///   - RegisterStudentInput.cs   → PlanId no input de cadastro de aluno
///   - StudentService.RegisterAsync() → valida que PlanId pertence à academia
///   - Program.cs SeedData       → seed de planos iniciais de teste
///   - Migration nova            → toda mudança de schema exige nova migration EF Core
///
/// CRUD DE PLANOS (FUTURO):
///   Atualmente planos são criados apenas via seed.
///   Para expor CRUD via GraphQL: criar PlanService, PlanResolver, PlanType
///   e registrar IPlanRepository no resolver.
/// </summary>
public class Plan
{
    public int Id { get; set; }

    /// <summary>
    /// FK para a academia proprietária deste plano.
    /// Garante o isolamento multitenant — um plano sempre pertence a uma academia.
    /// </summary>
    public int AcademyId { get; set; }

    /// <summary>
    /// Nome comercial do plano. Exibido em dropdowns de cadastro de aluno.
    /// Exemplos: "Mensal", "Trimestral", "Semestral", "Anual".
    /// Máximo 255 chars (configurado em PlanConfiguration.cs).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Valor do plano em moeda local (DECIMAL(10,2) no MySQL — exato, sem arredondamento).
    /// NÃO use float ou double para preço — há risco de erro de arredondamento.
    /// Ex: 89.90, 199.90, 349.90.
    /// Usado para ordenar planos do mais barato ao mais caro em PlanRepository.GetByAcademyIdAsync().
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Duração total do plano em dias inteiros.
    /// Convenções comuns: 30=mensal, 90=trimestral, 180=semestral, 365=anual.
    /// O frontend deve converter para exibição legível (ex: "30 dias" ou "1 mês").
    /// Não é usado como feature ML atualmente — possível feature futura (planos mais longos
    /// podem indicar maior comprometimento e menor probabilidade de churn).
    /// </summary>
    public int DurationDays { get; set; }

    // ── Navigation Properties (não são colunas — carregadas via Include) ─────────

    /// <summary>
    /// Academia proprietária do plano.
    /// Carregada via .Include(p => p.Academy) quando necessário.
    /// </summary>
    public Academy Academy { get; set; } = null!;

    /// <summary>
    /// Alunos atualmente matriculados neste plano.
    /// NÃO carregado por padrão.
    /// Se esta coleção não for vazia, o plano NÃO pode ser deletado (Restrict FK).
    /// Para migrar alunos de plano, usar StudentService.UpdateAsync() com novo PlanId.
    /// </summary>
    public ICollection<Student> Students { get; set; } = new List<Student>();
}
