using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Enums;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.Application.Services;

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
    /// Calcula KPIs do dashboard em 2 queries (students + latest predictions)
    /// em vez das 3 anteriores. Check-ins da semana mantidos em query separada
    /// pois requer filtro de data.
    /// </summary>
    public async Task<AcademyKPIsDto> GetKPIsAsync(int academyId, CancellationToken cancellationToken = default)
    {
        // Query 1: students (inclui contagem ativo/inativo)
        var allStudents = await _students.GetByAcademyIdAsync(academyId, null, cancellationToken);
        var totalStudents = allStudents.Count;
        var activeStudents = allStudents.Count(s => s.Active);

        // Query 2: última predição por aluno (inclui contagem de risco)
        var latestPredictions = await _predictions.GetLatestByAcademyAsync(academyId, cancellationToken);
        var highRisk = latestPredictions.Count(p => p.RiskLevel == RiskLevel.High);
        var mediumRisk = latestPredictions.Count(p => p.RiskLevel == RiskLevel.Medium);
        var lowRisk = latestPredictions.Count(p => p.RiskLevel == RiskLevel.Low);

        // Query 3: check-ins dos últimos 7 dias (filtro de data obriga query separada)
        var weekStart = DateTime.UtcNow.AddDays(-7);
        var checkInsThisWeek = await _checkIns.CountByAcademyAsync(academyId, weekStart, null, cancellationToken);

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
