using ProjectVital.Application.DTOs;

namespace ProjectVital.Application.Interfaces;

/// <summary>
/// Serviço de check-ins. Registra entradas e fornece histórico filtrável.
/// </summary>
public interface ICheckInService
{
    /// <summary>Registra um novo check-in com timestamp atual (UTC).</summary>
    Task<CheckInDto> RegisterAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Lista check-ins de um aluno em um intervalo opcional.</summary>
    Task<IReadOnlyList<CheckInDto>> ListByStudentAsync(
        int studentId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);
}
