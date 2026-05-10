using Microsoft.EntityFrameworkCore;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;
using ProjectVital.Domain.Interfaces;
using ProjectVital.Infrastructure.Persistence;

namespace ProjectVital.Infrastructure.Repositories;

public class PredictionRepository : Repository<Prediction>, IPredictionRepository
{
    public PredictionRepository(ProjectVitalDbContext db) : base(db) { }

    public async Task<Prediction?> GetLatestByStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        return await Set
            .AsNoTracking()
            .Where(p => p.StudentId == studentId)
            .OrderByDescending(p => p.PredictedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Prediction>> GetLatestByAcademyAsync(int academyId, CancellationToken cancellationToken = default)
    {
        // Para cada Student da academia, pega a Prediction mais recente.
        // Estratégia: GROUP BY StudentId pegando o MAX(PredictedAt), depois JOIN para recuperar a linha.
        var latestPerStudent = Db.Predictions
            .Where(p => p.Student.AcademyId == academyId)
            .GroupBy(p => p.StudentId)
            .Select(g => new { StudentId = g.Key, MaxAt = g.Max(p => p.PredictedAt) });

        return await Db.Predictions
            .AsNoTracking()
            .Where(p => latestPerStudent.Any(l => l.StudentId == p.StudentId && l.MaxAt == p.PredictedAt))
            .OrderByDescending(p => p.Probability)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<RiskLevel, int>> CountByRiskLevelAsync(int academyId, CancellationToken cancellationToken = default)
    {
        // Conta cada nível de risco baseado na predição MAIS RECENTE de cada aluno
        var latest = await GetLatestByAcademyAsync(academyId, cancellationToken);

        var grouped = latest
            .GroupBy(p => p.RiskLevel)
            .ToDictionary(g => g.Key, g => g.Count());

        // Garante que todas as chaves existam mesmo com contagem zero
        foreach (RiskLevel level in Enum.GetValues<RiskLevel>())
        {
            if (!grouped.ContainsKey(level))
                grouped[level] = 0;
        }

        return grouped;
    }
}
