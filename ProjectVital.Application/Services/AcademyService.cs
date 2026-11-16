using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Enums;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.Application.Services;

/// <summary>
/// Fornece dados sobre a academia: busca por Id e cálculo de KPIs do dashboard.
///
/// RESPONSABILIDADES:
///   - GetByIdAsync: retorna dados básicos da academia (usado internamente)
///   - GetKPIsAsync: agrega métricas em 3 queries SQL para o dashboard
///
/// DEPENDÊNCIAS:
///   - IAcademyRepository    → busca dados da academia
///   - IStudentRepository    → contagem de alunos ativos/inativos
///   - ICheckInRepository    → count de check-ins da semana
///   - IPredictionRepository → últimas predições para contagem de risco
///
/// SE MUDAR AQUI, IMPACTA:
///   - KPIResolver.AcademyKPIsAsync() → chama GetKPIsAsync()
///   - IAcademyService.cs → interface (manter sincronizado)
///   - AcademyKPIsDto.cs  → campos retornados ao frontend
///   - Se adicionar novos KPIs, verificar se precisam de novas queries
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

    /// <summary>
    /// Busca uma academia pelo Id. Retorna null se não encontrada.
    /// Usado internamente — os resolvers verificam Academy antes de operar.
    ///
    /// CHAMADO POR: KPIResolver (indiretamente via GetKPIsAsync)
    /// </summary>
    public async Task<AcademyDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var academy = await _academies.GetByIdAsync(id, cancellationToken);
        if (academy is null) return null;

        return new AcademyDto(academy.Id, academy.Name, academy.Address, academy.ManagerId, academy.CreatedAt);
    }

    /// <summary>
    /// Calcula os KPIs do dashboard em exatamente 3 queries SQL.
    ///
    /// QUERY 1 — Alunos (IStudentRepository.GetByAcademyIdAsync):
    ///   SELECT Students + User + Plan WHERE AcademyId = X
    ///   → totalStudents: count total
    ///   → activeStudents: count onde Active = true
    ///
    /// QUERY 2 — Predições (IPredictionRepository.GetLatestByAcademyAsync):
    ///   SELECT última predição por aluno (subquery MAX) ORDER BY Probability DESC
    ///   → highRisk, mediumRisk, lowRisk: contagem por nível de risco
    ///
    /// QUERY 3 — Check-ins (ICheckInRepository.CountByAcademyAsync):
    ///   SELECT COUNT(*) WHERE Timestamp >= (agora - 7 dias) AND AcademyId = X
    ///   → checkInsThisWeek
    ///   → avgWeeklyFrequency = checkInsThisWeek / activeStudents
    ///
    /// POR QUE 3 QUERIES E NÃO 1:
    ///   Check-ins precisam de filtro de data (não agregável com os outros joins).
    ///   Predições precisam de subquery MAX por aluno.
    ///   Fundir as 3 em uma query geraria SQL complexo e menos legível sem ganho real.
    ///
    /// SE ADICIONAR NOVOS KPIs:
    ///   - Verificar se cabe nas queries existentes ou precisa de uma 4ª
    ///   - Adicionar o campo em AcademyKPIsDto.cs
    ///   - Adicionar o campo em AcademyKPIsType.cs (tipo GraphQL)
    ///
    /// CHAMADO POR: KPIResolver.AcademyKPIsAsync()
    /// </summary>
    public async Task<AcademyKPIsDto> GetKPIsAsync(int academyId, CancellationToken cancellationToken = default)
    {
        // Query 1: todos os alunos — conta totais e ativos em memória (lista pequena)
        var allStudents = await _students.GetByAcademyIdAsync(academyId, null, cancellationToken);
        var totalStudents = allStudents.Count;
        var activeStudents = allStudents.Count(s => s.Active);

        // Query 2: última predição por aluno — agrupa por risco em memória
        var latestPredictions = await _predictions.GetLatestByAcademyAsync(academyId, cancellationToken);
        var highRisk   = latestPredictions.Count(p => p.RiskLevel == RiskLevel.High);
        var mediumRisk = latestPredictions.Count(p => p.RiskLevel == RiskLevel.Medium);
        var lowRisk    = latestPredictions.Count(p => p.RiskLevel == RiskLevel.Low);

        // Query 3: check-ins dos últimos 7 dias — obriga query separada por filtro de data
        var weekStart = DateTime.UtcNow.AddDays(-7);
        var checkInsThisWeek = await _checkIns.CountByAcademyAsync(academyId, weekStart, null, cancellationToken);

        // Frequência média: evitar divisão por zero quando não há alunos ativos
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
            Math.Round(avgWeeklyFrequency, 2), // 2 casas decimais para exibição no dashboard
            DateTime.UtcNow);                   // timestamp de geração dos KPIs
    }
}
