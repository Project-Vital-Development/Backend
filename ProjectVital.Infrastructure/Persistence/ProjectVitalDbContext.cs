using Microsoft.EntityFrameworkCore;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Entities;

namespace ProjectVital.Infrastructure.Persistence;

/// <summary>
/// Unidade central de trabalho do EF Core. Representa uma sessão com o banco MySQL.
/// Todos os repositórios recebem esta instância via injeção de dependência (Scoped).
///
/// RESPONSABILIDADES:
///   1. Expõe DbSet para cada entidade (tabela) do banco
///   2. Aplica configurações de mapeamento via IEntityTypeConfiguration
///   3. Configura Global Query Filters para soft delete (LGPD)
///
/// ESCOPO (Scoped no DI):
///   Uma instância por request HTTP. Isso significa que todos os repositórios
///   usados em um mesmo request compartilham o MESMO DbContext — logo, uma
///   única chamada SaveChangesAsync() salva alterações de TODOS os repositórios.
///   Isso é a base da atomicidade em StudentService.RegisterAsync().
///
/// SE ADICIONAR UMA NOVA ENTIDADE:
///   1. Criar a classe em Domain/Entities/
///   2. Adicionar DbSet aqui
///   3. Criar IEntityTypeConfiguration em Infrastructure/Persistence/Configurations/
///   4. Se a entidade tem soft delete, adicionar HasQueryFilter aqui
///   5. Criar migration: dotnet ef migrations add NomeDescritivo
///
/// SE MUDAR O SCHEMA (adicionar coluna, índice, FK):
///   Criar uma nova migration — NUNCA editar migration existente que já foi aplicada.
/// </summary>
public class ProjectVitalDbContext : DbContext
{
    private readonly ITenantContext _tenant;

    public ProjectVitalDbContext(DbContextOptions<ProjectVitalDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    // Lidas pelos filtros globais a cada query (o EF parametriza membros do DbContext;
    // valores capturados seriam congelados no modelo em cache).
    public bool TenantIsSystem => _tenant.IsSystem;
    public int TenantAcademyId => _tenant.AcademyId;

    // ── DbSets — cada um representa uma tabela no MySQL ───────────────────────────

    /// <summary>Tabela Users — contas de login (Admins, Managers e Students).</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Tabela Academies — tenant root. Uma linha por academia cadastrada.</summary>
    public DbSet<Academy> Academies => Set<Academy>();

    /// <summary>Tabela Plans — planos de mensalidade oferecidos por cada academia.</summary>
    public DbSet<Plan> Plans => Set<Plan>();

    /// <summary>Tabela Students — alunos matriculados. Sempre associado a um User e Academy.</summary>
    public DbSet<Student> Students => Set<Student>();

    /// <summary>
    /// Tabela CheckIns — registros de entrada na academia.
    /// Esta é a tabela com maior volume de dados — crescimento proporcional ao uso.
    /// Tem índice composto (StudentId, Timestamp) crítico para as queries do ML Service.
    /// </summary>
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();

    /// <summary>
    /// Tabela Predictions — histórico imutável de predições ML.
    /// Cada chamada a runPrediction() insere uma nova linha — nunca atualiza existentes.
    /// </summary>
    public DbSet<Prediction> Predictions => Set<Prediction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Carrega automaticamente TODOS os arquivos IEntityTypeConfiguration<T>
        // da assembly atual (Infrastructure). Isso inclui UserConfiguration,
        // StudentConfiguration, CheckInConfiguration, etc.
        // SE CRIAR UMA NOVA CONFIGURATION, ela é detectada automaticamente aqui.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProjectVitalDbContext).Assembly);

        // ── Global Query Filters — Soft Delete (LGPD Art.18) ─────────────────────
        // Filtra automaticamente registros com DeletedAt != null em TODAS as queries.
        // Efeito: EF Core adiciona "WHERE DeletedAt IS NULL" em todo SELECT dessas entidades.
        //
        // COMO DESATIVAR TEMPORARIAMENTE:
        //   Use .IgnoreQueryFilters() em uma query específica quando precisar ver excluídos.
        //   Ex: db.Users.IgnoreQueryFilters().Where(u => u.DeletedAt != null)
        //
        // SE ADICIONAR SOFT DELETE EM OUTRA ENTIDADE:
        //   1. Adicionar DeletedAt (DateTime?) + SoftDelete() na entidade
        //   2. Adicionar HasQueryFilter aqui
        //   3. Criar migration para adicionar a coluna
        modelBuilder.Entity<User>().HasQueryFilter(u => u.DeletedAt == null);

        // ── Global Query Filters — Isolamento por academia (multi-tenant) ────────
        // Segunda barreira, além das checagens nos resolvers: mesmo que um resolver
        // esqueça de validar a academia, o banco só devolve linhas da academia do JWT.
        // EF Core 8 aceita UM filtro por entidade — soft delete e tenant ficam juntos.
        // NUNCA usar IgnoreQueryFilters() em código acionado por requisição HTTP.
        modelBuilder.Entity<Student>().HasQueryFilter(s =>
            s.DeletedAt == null && (TenantIsSystem || s.AcademyId == TenantAcademyId));
        modelBuilder.Entity<Academy>().HasQueryFilter(a =>
            TenantIsSystem || a.Id == TenantAcademyId);
        modelBuilder.Entity<Plan>().HasQueryFilter(p =>
            TenantIsSystem || p.AcademyId == TenantAcademyId);
        modelBuilder.Entity<CheckIn>().HasQueryFilter(c =>
            TenantIsSystem || c.Student.AcademyId == TenantAcademyId);
        modelBuilder.Entity<Prediction>().HasQueryFilter(p =>
            TenantIsSystem || p.Student.AcademyId == TenantAcademyId);

        base.OnModelCreating(modelBuilder);
    }
}
