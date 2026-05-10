using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectVital.Domain.Entities;

namespace ProjectVital.Infrastructure.Persistence.Configurations;

public class AcademyConfiguration : IEntityTypeConfiguration<Academy>
{
    public void Configure(EntityTypeBuilder<Academy> builder)
    {
        builder.ToTable("Academies");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedOnAdd();

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.Address)
            .HasMaxLength(500);

        builder.Property(a => a.ManagerId)
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // FK Academies.ManagerId → Users.Id (1:1 — cada Manager gerencia uma Academy)
        builder.HasOne(a => a.Manager)
            .WithOne(u => u.ManagedAcademy)
            .HasForeignKey<Academy>(a => a.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
