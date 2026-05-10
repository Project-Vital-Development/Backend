using Microsoft.EntityFrameworkCore;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Interfaces;
using ProjectVital.Infrastructure.Persistence;

namespace ProjectVital.Infrastructure.Repositories;

public class AcademyRepository : Repository<Academy>, IAcademyRepository
{
    public AcademyRepository(ProjectVitalDbContext db) : base(db) { }

    public async Task<Academy?> GetByManagerIdAsync(int managerId, CancellationToken cancellationToken = default)
    {
        return await Set
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ManagerId == managerId, cancellationToken);
    }

    public async Task<Academy?> GetWithPlansAsync(int id, CancellationToken cancellationToken = default)
    {
        return await Set
            .AsNoTracking()
            .Include(a => a.Plans)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }
}
