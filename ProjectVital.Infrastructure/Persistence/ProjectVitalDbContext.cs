using Microsoft.EntityFrameworkCore;
using ProjectVital.Domain.Entities;

namespace ProjectVital.Infrastructure.Persistence;

/// <summary>
/// Contexto principal do EF Core. Code-First: o schema do MySQL é gerado a partir
/// destas configurações via migrations. NUNCA editar o banco diretamente — sempre via migration.
/// </summary>
public class ProjectVitalDbContext : DbContext
{
    public ProjectVitalDbContext(DbContextOptions<ProjectVitalDbContext> options) : base(options)
    {
    }

    // ── DbSets ───────────────────────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<Academy> Academies => Set<Academy>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<Prediction> Predictions => Set<Prediction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Aplica todas as IEntityTypeConfiguration<T> deste assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProjectVitalDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
