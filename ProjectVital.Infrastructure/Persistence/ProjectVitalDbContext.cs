using Microsoft.EntityFrameworkCore;
using ProjectVital.Domain.Entities;

namespace ProjectVital.Infrastructure.Persistence;

public class ProjectVitalDbContext : DbContext
{
    public ProjectVitalDbContext(DbContextOptions<ProjectVitalDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Academy> Academies => Set<Academy>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<Prediction> Predictions => Set<Prediction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProjectVitalDbContext).Assembly);

        // ── Global Query Filters (LGPD soft delete) ──────────────────────────
        // Entidades soft-deletadas são invisíveis em todas as queries normais.
        // Use .IgnoreQueryFilters() quando precisar acessar registros excluídos.
        modelBuilder.Entity<User>().HasQueryFilter(u => u.DeletedAt == null);
        modelBuilder.Entity<Student>().HasQueryFilter(s => s.DeletedAt == null);

        base.OnModelCreating(modelBuilder);
    }
}
