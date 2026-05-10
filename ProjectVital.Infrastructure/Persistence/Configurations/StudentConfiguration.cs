using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectVital.Domain.Entities;

namespace ProjectVital.Infrastructure.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd();

        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.AcademyId).IsRequired();
        builder.Property(s => s.PlanId).IsRequired();

        builder.Property(s => s.EnrollmentDate)
            .IsRequired();

        builder.Property(s => s.Active)
            .IsRequired()
            .HasDefaultValue(true);

        // ── FKs ──────────────────────────────────────────────────────────────
        // Student.UserId → User.Id (1:1) — Restrict para preservar histórico
        builder.HasOne(s => s.User)
            .WithOne(u => u.Student)
            .HasForeignKey<Student>(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Student.AcademyId → Academy.Id (N:1)
        builder.HasOne(s => s.Academy)
            .WithMany(a => a.Students)
            .HasForeignKey(s => s.AcademyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Student.PlanId → Plan.Id (N:1)
        builder.HasOne(s => s.Plan)
            .WithMany(p => p.Students)
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Índices ──────────────────────────────────────────────────────────
        // Listagem de alunos ativos por academia (consulta frequente do dashboard)
        builder.HasIndex(s => new { s.AcademyId, s.Active })
            .HasDatabaseName("idx_academy_students");
    }
}
