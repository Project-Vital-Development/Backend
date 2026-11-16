using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;

namespace ProjectVital.Domain.Interfaces;

/// <summary>
/// Repositório de predições. Histórico imutável — nunca atualizar uma predição existente.
/// </summary>
public interface IPredictionRepository : IRepository<Prediction>
{
    /// <summary>Última predição registrada para um aluno (mais recente).</summary>
    Task<Prediction?> GetLatestByStudentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista as últimas predições de cada aluno de uma academia (uma por aluno).
    /// Útil para o dashboard de risco geral.
    /// </summary>
    Task<IReadOnlyList<Prediction>> GetLatestByAcademyAsync(int academyId, CancellationToken cancellationToken = default);

    /// <summary>Conta alunos da academia agrupados pelo seu nível de risco mais recente.</summary>
    Task<IReadOnlyDictionary<RiskLevel, int>> CountByRiskLevelAsync(int academyId, CancellationToken cancellationToken = default);
}
