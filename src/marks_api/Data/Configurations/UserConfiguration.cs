using marks_api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace marks_api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", table =>
        {
            table.HasCheckConstraint(
                "ck_users_role",
                "role IN ('Teacher', 'Student')"
            );
        });

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.Role)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(u => u.Username)
            .IsUnique();

        builder.HasOne(u => u.Student)
            .WithMany(s => s.Users)
            .HasForeignKey(u => u.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}