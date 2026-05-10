using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectVital.Domain.Entities;

namespace ProjectVital.Infrastructure.Persistence.Configurations;

public class CheckInConfiguration : IEntityTypeConfiguration<CheckIn>
{
    public void Configure(EntityTypeBuilder<CheckIn> builder)
    {
        builder.ToTable("CheckIns");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd();

        builder.Property(c => c.StudentId)
            .IsRequired();

        builder.Property(c => c.Timestamp)
            .IsRequired();

        builder.Property(c => c.DurationMinutes);

        // FK CheckIns.StudentId → Students.Id
        builder.HasOne(c => c.Student)
            .WithMany(s => s.CheckIns)
            .HasForeignKey(c => c.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Índice CRÍTICO ────────────────────────────────────────────────────
        // (StudentId, Timestamp) é a query principal do feature engineering ML.
        // Sem este índice, o pipeline de predição fica O(N) sobre toda a tabela.
        builder.HasIndex(c => new { c.StudentId, c.Timestamp })
            .HasDatabaseName("idx_student_timestamp");
    }
}
