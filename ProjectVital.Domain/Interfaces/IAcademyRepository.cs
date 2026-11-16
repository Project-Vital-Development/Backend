using ProjectVital.Domain.Entities;

namespace ProjectVital.Domain.Interfaces;

/// <summary>
/// Repositório de academias (tenant root).
/// </summary>
public interface IAcademyRepository : IRepository<Academy>
{
    /// <summary>Retorna a academia gerenciada por um determinado usuário (Manager).</summary>
    Task<Academy?> GetByManagerIdAsync(int managerId, CancellationToken cancellationToken = default);

    /// <summary>Carrega uma academia com seus planos.</summary>
    Task<Academy?> GetWithPlansAsync(int id, CancellationToken cancellationToken = default);
}
