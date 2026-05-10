using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;

namespace ProjectVital.Infrastructure.Persistence.Configurations;

public class PredictionConfiguration : IEntityTypeConfiguration<Prediction>
{
    public void Configure(EntityTypeBuilder<Prediction> builder)
    {
        builder.ToTable("Predictions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd();

        builder.Property(p => p.StudentId)
            .IsRequired();

        // FLOAT no schema do CLAUDE.md — equivalente a double em C#
        builder.Property(p => p.Probability)
            .IsRequired();

        // ENUM('low', 'medium', 'high') no MySQL — armazenado como string lowercase
        builder.Property(p => p.RiskLevel)
            .IsRequired()
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => (RiskLevel)Enum.Parse(typeof(RiskLevel), v, true))
            .HasMaxLength(10);

        builder.Property(p => p.ModelVersion)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.PredictedAt)
            .IsRequired();

        // FK Predictions.StudentId → Students.Id
        builder.HasOne(p => p.Student)
            .WithMany(s => s.Predictions)
            .HasForeignKey(p => p.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índice — busca da última predição por aluno (DESC sobre PredictedAt)
        builder.HasIndex(p => new { p.StudentId, p.PredictedAt })
            .HasDatabaseName("idx_student_predictions");
    }
}
