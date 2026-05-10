using Microsoft.EntityFrameworkCore;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Interfaces;
using ProjectVital.Infrastructure.Persistence;

namespace ProjectVital.Infrastructure.Repositories;

public class PlanRepository : Repository<Plan>, IPlanRepository
{
    public PlanRepository(ProjectVitalDbContext db) : base(db) { }

    public async Task<IReadOnlyList<Plan>> GetByAcademyIdAsync(int academyId, CancellationToken cancellationToken = default)
    {
        return await Set
            .AsNoTracking()
            .Where(p => p.AcademyId == academyId)
            .OrderBy(p => p.Price)
            .ToListAsync(cancellationToken);
    }
}
