using ProjectVital.Application.DTOs;

namespace ProjectVital.Application.Interfaces;

/// <summary>
/// Serviço de academias. Provê informações agregadas (KPIs) e gestão básica.
/// </summary>
public interface IAcademyService
{
    Task<AcademyDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Calcula os indicadores agregados (KPIs) de uma academia para o dashboard.</summary>
    Task<AcademyKPIsDto> GetKPIsAsync(int academyId, CancellationToken cancellationToken = default);
}
