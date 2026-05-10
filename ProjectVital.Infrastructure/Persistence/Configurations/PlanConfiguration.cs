using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectVital.Domain.Entities;

namespace ProjectVital.Infrastructure.Persistence.Configurations;

public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("Plans");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd();

        builder.Property(p => p.AcademyId)
            .IsRequired();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(255);

        // DECIMAL(10,2) conforme schema do CLAUDE.md
        builder.Property(p => p.Price)
            .IsRequired()
            .HasPrecision(10, 2);

        builder.Property(p => p.DurationDays)
            .IsRequired();

        // FK Plans.AcademyId → Academies.Id
        builder.HasOne(p => p.Academy)
            .WithMany(a => a.Plans)
            .HasForeignKey(p => p.AcademyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
