using Microsoft.EntityFrameworkCore;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Interfaces;
using ProjectVital.Infrastructure.Persistence;

namespace ProjectVital.Infrastructure.Repositories;

/// <summary>
/// Repositório de planos de mensalidade — acessa a tabela Plans no MySQL.
/// Herda operações CRUD base de Repository&lt;Plan&gt;.
///
/// REGISTRADO EM: Program.cs → services.AddScoped&lt;IPlanRepository, PlanRepository&gt;()
///
/// PLANOS SÃO POR TENANT (AcademyId):
///   Cada academia tem seus próprios planos (Mensal, Trimestral, etc.).
///   Um Manager da Academia A não deve ver planos da Academia B.
///
/// CONSUMERS ATUAIS:
///   - StudentService.RegisterAsync() → valida PlanId via AcademyRepository.GetWithPlansAsync
///   - AcademyRepository.GetWithPlansAsync() → inclui planos via navigation property
///   - (Futuro) PlanResolver → CRUD de planos via GraphQL
///
/// SE CRIAR CRUD DE PLANOS NO GRAPHQL:
///   Criar PlanService, PlanResolver e PlanType.
///   Injetar IPlanRepository no PlanService.
/// </summary>
public class PlanRepository : Repository<Plan>, IPlanRepository
{
    public PlanRepository(ProjectVitalDbContext db) : base(db) { }

    /// <summary>
    /// Lista todos os planos de uma academia ordenados por preço crescente.
    ///
    /// ORDENAÇÃO POR PREÇO: útil para exibir planos do mais barato ao mais caro
    ///   em formulários de cadastro de alunos (select dropdown).
    ///
    /// AsNoTracking: apenas leitura — não precisamos rastrear planos para escrita.
    ///   Mais performático que tracking para listas grandes.
    ///
    /// USADO POR: (futuro) PlanService ao listar planos para formulário de cadastro de aluno.
    /// </summary>
    public async Task<IReadOnlyList<Plan>> GetByAcademyIdAsync(int academyId, CancellationToken cancellationToken = default)
    {
        return await Set
            .AsNoTracking()
            .Where(p => p.AcademyId == academyId)
            .OrderBy(p => p.Price)
            .ToListAsync(cancellationToken);
    }
}
