using ProjectVital.Application.DTOs;

namespace ProjectVital.Application.Interfaces;

/// <summary>
/// Contrato do serviço de academias.
/// Gerencia dados da academia (tenant root) e cálculo de KPIs operacionais.
///
/// IMPLEMENTAÇÃO: AcademyService.cs (Application/Services/AcademyService.cs)
/// REGISTRADO EM: Program.cs → services.AddScoped&lt;IAcademyService, AcademyService&gt;()
///
/// ACADEMIA É O TENANT ROOT:
///   Todos os dados do sistema (alunos, check-ins, predições) são isolados por AcademyId.
///   Esta interface fornece as operações no nível do tenant, não dos seus dados.
///
/// CONSUMERS DESTA INTERFACE:
///   - KPIResolver.AcademyKPIsAsync() — query do dashboard
///   - Possível uso futuro: resolver de Academy CRUD (ainda não implementado)
///
/// SE ADICIONAR CRUD DE ACADEMIA:
///   Adicionar métodos aqui (CreateAsync, UpdateAsync, DeleteAsync).
///   Criar AcademyMutationResolver correspondente.
/// </summary>
public interface IAcademyService
{
    /// <summary>
    /// Busca uma academia pelo Id.
    /// Retorna null se não existir ou estiver soft-deleted.
    ///
    /// USO ATUAL: não usado por nenhum resolver (sem query "academy(id)" no schema ainda).
    ///   Disponível para uso futuro em resolvers de gestão de academia.
    /// </summary>
    Task<AcademyDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcula os KPIs operacionais da academia para o dashboard principal.
    ///
    /// O QUE RETORNA (AcademyKPIsDto):
    ///   - totalStudents, activeStudents: contagens básicas de alunos
    ///   - highRiskCount, mediumRiskCount, lowRiskCount: distribuição de risco
    ///     baseada na última predição de cada aluno ativo
    ///   - checkInsThisWeek: total de entradas nos últimos 7 dias
    ///   - averageWeeklyFrequency: checkInsThisWeek / activeStudents
    ///   - generatedAt: timestamp do cálculo (UTC)
    ///
    /// PERFORMANCE: usa 2 queries ao banco (não N queries por aluno).
    ///   Ver AcademyService.GetKPIsAsync() para a estratégia de queries.
    ///
    /// CHAMADO POR: KPIResolver.AcademyKPIsAsync()
    /// </summary>
    Task<AcademyKPIsDto> GetKPIsAsync(int academyId, CancellationToken cancellationToken = default);
}
