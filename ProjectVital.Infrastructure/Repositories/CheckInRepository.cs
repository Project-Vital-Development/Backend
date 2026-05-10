using Microsoft.EntityFrameworkCore;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Interfaces;
using ProjectVital.Infrastructure.Persistence;

namespace ProjectVital.Infrastructure.Repositories;

public class CheckInRepository : Repository<CheckIn>, ICheckInRepository
{
    public CheckInRepository(ProjectVitalDbContext db) : base(db) { }

    public async Task<IReadOnlyList<CheckIn>> GetByStudentAsync(
        int studentId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = Set
            .AsNoTracking()
            .Where(c => c.StudentId == studentId);

        if (from.HasValue)
            query = query.Where(c => c.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(c => c.Timestamp <= to.Value);

        return await query
            .OrderByDescending(c => c.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByStudentAsync(
        int studentId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = Set
            .Where(c => c.StudentId == studentId);

        if (from.HasValue) query = query.Where(c => c.Timestamp >= from.Value);
        if (to.HasValue)   query = query.Where(c => c.Timestamp <= to.Value);

        return await query.CountAsync(cancellationToken);
    }

    public async Task<int> CountByAcademyAsync(
        int academyId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        // JOIN implícito via navegação — Pomelo gera SQL eficiente usando idx_student_timestamp
        var query = Db.CheckIns
            .Where(c => c.Student.AcademyId == academyId);

        if (from.HasValue) query = query.Where(c => c.Timestamp >= from.Value);
        if (to.HasValue)   query = query.Where(c => c.Timestamp <= to.Value);

        return await query.CountAsync(cancellationToken);
    }
}
