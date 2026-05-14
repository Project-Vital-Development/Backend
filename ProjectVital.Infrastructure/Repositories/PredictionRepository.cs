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

    /// <summary>
    /// Retorna a predição mais recente de cada aluno da academia.
    /// Usa subquery com MAX(PredictedAt) para evitar N+1: uma única query SQL.
    /// </summary>
    public async Task<IReadOnlyList<Prediction>> GetLatestByAcademyAsync(int academyId, CancellationToken cancellationToken = default)
    {
        // Subquery: para cada StudentId, encontra o timestamp máximo
        var latestTimestamps = Db.Predictions
            .Where(p => p.Student.AcademyId == academyId)
            .GroupBy(p => p.StudentId)
            .Select(g => new { StudentId = g.Key, MaxAt = g.Max(p => p.PredictedAt) });

        // JOIN entre Predictions e os timestamps máximos — uma única query SQL
        return await Db.Predictions
            .AsNoTracking()
            .Where(p => p.Student.AcademyId == academyId)
            .Join(latestTimestamps,
                p => new { p.StudentId, At = p.PredictedAt },
                l => new { l.StudentId, At = l.MaxAt },
                (p, _) => p)
            .OrderByDescending(p => p.Probability)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<RiskLevel, int>> CountByRiskLevelAsync(int academyId, CancellationToken cancellationToken = default)
    {
        var latest = await GetLatestByAcademyAsync(academyId, cancellationToken);

        var grouped = latest
            .GroupBy(p => p.RiskLevel)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (RiskLevel level in Enum.GetValues<RiskLevel>())
        {
            if (!grouped.ContainsKey(level))
                grouped[level] = 0;
        }

        return grouped;
    }
}
