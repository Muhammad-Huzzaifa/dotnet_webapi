using marks_api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace marks_api.Data.Configurations;

public class EnrollmentConfiguration
    : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("enrollments", table =>
        {
            table.HasCheckConstraint(
                "ck_enrollments_mark",
                "mark IS NULL OR mark BETWEEN 0 AND 100"
            );
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Mark)
            .IsRequired(false);

        builder.Property(e => e.EnrolledAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Student relationship
        builder.HasOne(e => e.Student)
            .WithMany(s => s.Enrollments)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Course relationship
        builder.HasOne(e => e.Course)
            .WithMany(c => c.Enrollments)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prevent duplicate enrollments
        builder.HasIndex(e => new
        {
            e.StudentId,
            e.CourseId
        }).IsUnique();
    }
}