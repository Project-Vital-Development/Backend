using Microsoft.EntityFrameworkCore;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;
using ProjectVital.Domain.Interfaces;
using ProjectVital.Infrastructure.Persistence;

namespace ProjectVital.Infrastructure.Repositories;

/// <summary>
/// Repositório de Prediction com otimizações para o padrão "última predição por aluno".
/// O caso de uso mais comum é: "mostre o risco atual de todos os alunos da academia"
/// — isso exige uma query que retorna SOMENTE a predição mais recente por aluno.
/// </summary>
public class PredictionRepository : Repository<Prediction>, IPredictionRepository
{
    public PredictionRepository(ProjectVitalDbContext db) : base(db) { }

    /// <summary>
    /// Retorna a predição mais recente de um aluno específico.
    /// Simples ORDER BY + LIMIT 1 — eficiente com o índice (StudentId, PredictedAt).
    ///
    /// CHAMADO POR:
    ///   - PredictionService.GetLatestByStudentAsync()  → detalhe de predição individual
    ///   - StudentService.GetByIdAsync() (via PredictionRepository diretamente)
    /// </summary>
    public async Task<Prediction?> GetLatestByStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        return await Set
            .AsNoTracking()
            .Where(p => p.StudentId == studentId)
            .OrderByDescending(p => p.PredictedAt) // mais recente primeiro
            .FirstOrDefaultAsync(cancellationToken); // LIMIT 1
    }

    /// <summary>
    /// Retorna a predição mais recente de CADA aluno de uma academia.
    /// Esta é a query mais crítica do dashboard — usada para exibir o risco de todos os alunos.
    ///
    /// ESTRATÉGIA ANTI-N+1:
    ///   N+1 seria: buscar todos os alunos → para cada um, buscar a última predição.
    ///   Com 100 alunos → 101 queries. INACEITÁVEL.
    ///
    ///   Solução: subquery com MAX(PredictedAt) por StudentId → JOIN com Predictions.
    ///   Resulta em UMA ÚNICA query SQL:
    ///
    ///   SELECT p.*
    ///   FROM Predictions p
    ///   INNER JOIN (
    ///     SELECT StudentId, MAX(PredictedAt) AS MaxAt
    ///     FROM Predictions
    ///     WHERE Student.AcademyId = @academyId
    ///     GROUP BY StudentId
    ///   ) latest ON p.StudentId = latest.StudentId AND p.PredictedAt = latest.MaxAt
    ///   WHERE p.Student.AcademyId = @academyId
    ///   ORDER BY p.Probability DESC
    ///
    /// RESULTADO ordenado por Probability DESC: alunos de maior risco aparecem primeiro.
    ///
    /// SE DOIS ALUNOS TIVEREM O MESMO MAX(PredictedAt):
    ///   O JOIN retorna ambas — caso extremamente raro, aceitável no contexto atual.
    ///
    /// CHAMADO POR:
    ///   - PredictionService.ListLatestByAcademyAsync() → dashboard de risco
    ///   - AcademyService.GetKPIsAsync() → contagem high/medium/low
    ///   - PredictionRepository.CountByRiskLevelAsync() → contagem por nível
    /// </summary>
    public async Task<IReadOnlyList<Prediction>> GetLatestByAcademyAsync(int academyId, CancellationToken cancellationToken = default)
    {
        // Subquery: para cada StudentId, encontra o timestamp da predição mais recente
        var latestTimestamps = Db.Predictions
            .Where(p => p.Student.AcademyId == academyId) // filtra pela academia via JOIN
            .GroupBy(p => p.StudentId)
            .Select(g => new { StudentId = g.Key, MaxAt = g.Max(p => p.PredictedAt) });

        // JOIN principal: une Predictions com a subquery pelos dois campos (StudentId + timestamp)
        return await Db.Predictions
            .AsNoTracking()
            .Where(p => p.Student.AcademyId == academyId)
            .Join(latestTimestamps,
                p => new { p.StudentId, At = p.PredictedAt },    // chave do lado esquerdo
                l => new { l.StudentId, At = l.MaxAt },           // chave do lado direito
                (p, _) => p)                                       // projeta apenas Prediction
            .OrderByDescending(p => p.Probability)                 // maior risco primeiro
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Conta alunos da academia agrupados pelo nível de risco da última predição.
    /// Garante que todos os níveis (Low/Medium/High) estejam no resultado — mesmo com 0.
    ///
    /// COMO FUNCIONA:
    ///   1. Chama GetLatestByAcademyAsync() (reutiliza a query otimizada)
    ///   2. Agrupa em memória por RiskLevel
    ///   3. Garante que todos os 3 níveis existam no dicionário (preenche com 0 se ausente)
    ///
    /// POR QUE COMPLETAR COM ZERO:
    ///   Se nenhum aluno é High Risk, o GroupBy não gera entrada para High.
    ///   O KPI dashboard espera sempre os 3 níveis — sem isso, poderia dar KeyNotFoundException.
    ///
    /// CHAMADO POR: AcademyService.GetKPIsAsync() (futuro — atualmente não usado diretamente)
    /// </summary>
    public async Task<IReadOnlyDictionary<RiskLevel, int>> CountByRiskLevelAsync(int academyId, CancellationToken cancellationToken = default)
    {
        // Reutiliza a query otimizada de últimas predições
        var latest = await GetLatestByAcademyAsync(academyId, cancellationToken);

        // Agrupa em memória — a lista já é pequena (uma entrada por aluno)
        var grouped = latest
            .GroupBy(p => p.RiskLevel)
            .ToDictionary(g => g.Key, g => g.Count());

        // Garante que todos os 3 valores do enum estejam presentes com 0 se não houve predições
        foreach (RiskLevel level in Enum.GetValues<RiskLevel>())
        {
            if (!grouped.ContainsKey(level))
                grouped[level] = 0;
        }

        return grouped;
    }
}
