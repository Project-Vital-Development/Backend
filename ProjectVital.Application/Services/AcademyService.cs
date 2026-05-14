using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Enums;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.Application.Services;

/// <summary>
/// Implementação do serviço de academias.
/// Fornece dados agregados (KPIs) para o dashboard do gestor.
/// </summary>
public class AcademyService : IAcademyService
{
    private readonly IAcademyRepository _academies;
    private readonly IStudentRepository _students;
    private readonly ICheckInRepository _checkIns;
    private readonly IPredictionRepository _predictions;

    public AcademyService(
        IAcademyRepository academies,
        IStudentRepository students,
        ICheckInRepository checkIns,
        IPredictionRepository predictions)
    {
        _academies = academies;
        _students = students;
        _checkIns = checkIns;
        _predictions = predictions;
    }

    public async Task<AcademyDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var academy = await _academies.GetByIdAsync(id, cancellationToken);
        if (academy is null) return null;

        return new AcademyDto(academy.Id, academy.Name, academy.Address, academy.ManagerId, academy.CreatedAt);
    }

    /// <summary>
    /// Calcula KPIs do dashboard: contagens de risco, check-ins da semana,
    /// frequência média semanal e distribuição de status dos alunos.
    /// </summary>
    public async Task<AcademyKPIsDto> GetKPIsAsync(int academyId, CancellationToken cancellationToken = default)
    {
        var allStudents = await _students.GetByAcademyIdAsync(academyId, null, cancellationToken);
        var totalStudents = allStudents.Count;
        var activeStudents = allStudents.Count(s => s.Active);

        // Distribuição de risco baseada na última predição de cada aluno
        var riskCounts = await _predictions.CountByRiskLevelAsync(academyId, cancellationToken);
        var highRisk = riskCounts.GetValueOrDefault(RiskLevel.High, 0);
        var mediumRisk = riskCounts.GetValueOrDefault(RiskLevel.Medium, 0);
        var lowRisk = riskCounts.GetValueOrDefault(RiskLevel.Low, 0);

        // Check-ins dos últimos 7 dias
        var weekStart = DateTime.UtcNow.AddDays(-7);
        var checkInsThisWeek = await _checkIns.CountByAcademyAsync(academyId, weekStart, null, cancellationToken);

        // Frequência média semanal = check-ins / alunos ativos (evita divisão por zero)
        var avgWeeklyFrequency = activeStudents > 0
            ? (double)checkInsThisWeek / activeStudents
            : 0.0;

        return new AcademyKPIsDto(
            academyId,
            totalStudents,
            activeStudents,
            highRisk,
            mediumRisk,
            lowRisk,
            checkInsThisWeek,
            Math.Round(avgWeeklyFrequency, 2),
            DateTime.UtcNow);
    }
}
