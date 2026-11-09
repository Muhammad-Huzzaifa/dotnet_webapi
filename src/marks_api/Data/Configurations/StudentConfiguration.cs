using marks_api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace marks_api.Data.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("students", table =>
        {
            table.HasCheckConstraint(
                "ck_students_email_lowercase",
                "email = lower(email)"
            );
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.RegistrationNo)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(s => s.FullName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Email)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(s => s.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(s => s.RegistrationNo)
            .IsUnique();

        builder.HasIndex(s => s.Email)
            .IsUnique();
    }
}