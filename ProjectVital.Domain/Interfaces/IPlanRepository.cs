using ProjectVital.Domain.Entities;

namespace ProjectVital.Domain.Interfaces;

/// <summary>
/// Repositório de planos de mensalidade. Sempre escopado a uma academia.
/// </summary>
public interface IPlanRepository : IRepository<Plan>
{
    /// <summary>Lista os planos disponíveis em uma academia.</summary>
    Task<IReadOnlyList<Plan>> GetByAcademyIdAsync(int academyId, CancellationToken cancellationToken = default);
}
