using Microsoft.EntityFrameworkCore;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Interfaces;
using ProjectVital.Infrastructure.Persistence;

namespace ProjectVital.Infrastructure.Repositories;

public class StudentRepository : Repository<Student>, IStudentRepository
{
    public StudentRepository(ProjectVitalDbContext db) : base(db) { }

    public async Task<IReadOnlyList<Student>> GetByAcademyIdAsync(
        int academyId,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        var query = Set
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Plan)
            .Where(s => s.AcademyId == academyId);

        if (activeOnly.HasValue)
            query = query.Where(s => s.Active == activeOnly.Value);

        return await query
            .OrderBy(s => s.User.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Student?> GetWithRelationsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await Set
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Academy)
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Student?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await Set
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
    }
}
