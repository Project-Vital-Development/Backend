using Microsoft.EntityFrameworkCore;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Interfaces;
using ProjectVital.Infrastructure.Persistence;

namespace ProjectVital.Infrastructure.Repositories;

/// <summary>
/// Repositório de academias — acessa a tabela Academies no MySQL.
/// Herda operações CRUD base de Repository&lt;Academy&gt;.
///
/// REGISTRADO EM: Program.cs → services.AddScoped&lt;IAcademyRepository, AcademyRepository&gt;()
///
/// ACADEMIA É O TENANT ROOT:
///   Todos os dados do sistema (alunos, planos, check-ins, predições) são isolados
///   por AcademyId. Este repositório acessa a entidade raiz do tenant.
///
/// CONSUMERS:
///   - AuthService.LoginAsync() → usa GetByManagerIdAsync para obter AcademyId do Manager
///   - AcademyService.GetByIdAsync() → usa GetByIdAsync herdado
///   - AcademyService.GetKPIsAsync() → usa GetByIdAsync herdado para verificar existência
///   - StudentService.RegisterAsync() → usa GetWithPlansAsync para verificar se planId pertence à academia
/// </summary>
public class AcademyRepository : Repository<Academy>, IAcademyRepository
{
    public AcademyRepository(ProjectVitalDbContext db) : base(db) { }

    /// <summary>
    /// Busca a academia gerenciada por um Manager específico.
    /// Usado por AuthService para incluir AcademyId no JWT do Manager após login.
    ///
    /// MODELO ATUAL: um Manager por academia (ManagerId é 1:1 com Academia).
    ///   Se no futuro um Manager puder gerenciar múltiplas academias, este método
    ///   precisaria retornar uma lista (IReadOnlyList&lt;Academy&gt;).
    ///
    /// AsNoTracking: não precisamos rastrear — apenas leitura para incluir no token.
    ///
    /// RETORNA NULL: se o Manager não tiver academia vinculada.
    ///   AuthService.LoginAsync() lida com null → AcademyId fica null no token.
    /// </summary>
    public async Task<Academy?> GetByManagerIdAsync(int managerId, CancellationToken cancellationToken = default)
    {
        return await Set
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ManagerId == managerId, cancellationToken);
    }

    /// <summary>
    /// Busca uma academia com seus planos carregados (eager loading).
    /// Usado para verificar se um PlanId pertence a uma academia específica.
    ///
    /// INCLUDE(a => a.Plans): gera JOIN com a tabela Plans.
    ///   Necessário para StudentService.RegisterAsync() verificar que o PlanId
    ///   passado no input realmente pertence à academia do aluno.
    ///
    /// AsNoTracking: apenas leitura para validação.
    ///
    /// USADO POR: StudentService.RegisterAsync() para validar PlanId.
    /// </summary>
    public async Task<Academy?> GetWithPlansAsync(int id, CancellationToken cancellationToken = default)
    {
        return await Set
            .AsNoTracking()
            .Include(a => a.Plans)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }
}
