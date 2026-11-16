using ProjectVital.Domain.Entities;

namespace ProjectVital.Domain.Interfaces;

/// <summary>
/// Repositório de check-ins. Acesso otimizado pelo índice composto (StudentId, Timestamp).
/// </summary>
public interface ICheckInRepository : IRepository<CheckIn>
{
    /// <summary>
    /// Lista check-ins de um aluno, opcionalmente filtrando por intervalo de datas.
    /// Resultados ordenados por Timestamp descendente.
    /// </summary>
    /// <param name="track">true = entidades rastreadas pelo EF (para alterar e salvar, ex.: check-out).</param>
    Task<IReadOnlyList<CheckIn>> GetByStudentAsync(
        int studentId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default,
        bool track = false);

    /// <summary>Conta check-ins de um aluno em um intervalo (usado em KPIs).</summary>
    Task<int> CountByStudentAsync(
        int studentId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>Conta check-ins de toda uma academia em um intervalo.</summary>
    Task<int> CountByAcademyAsync(
        int academyId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);
}
