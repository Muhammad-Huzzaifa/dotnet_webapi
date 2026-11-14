using marks_api.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace marks_api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        // Avoid inserting the initial dataset more than once.
        if (await context.Students.AnyAsync(cancellationToken) ||
            await context.Courses.AnyAsync(cancellationToken) ||
            await context.Enrollments.AnyAsync(cancellationToken) ||
            await context.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;

        // Students
        var students = new List<Student>
        {
            new()
            {
                RegistrationNo = "BSAI-2023-001",
                FullName = "Ali Hassan",
                Email = "ali.hassan@example.com",
                CreatedAtUtc = now
            },
            new()
            {
                RegistrationNo = "BSAI-2023-002",
                FullName = "Sara Ahmed",
                Email = "sara.ahmed@example.com",
                CreatedAtUtc = now
            }
        };

        // Courses
        var courses = new List<Course>
        {
            new()
            {
                Code = "CS-301",
                Title = "Advanced Programming",
                CreditHours = 3,
                CreatedAtUtc = now
            },
            new()
            {
                Code = "CS-302",
                Title = "Database Systems",
                CreditHours = 3,
                CreatedAtUtc = now
            },
            new()
            {
                Code = "AI-401",
                Title = "Machine Learning",
                CreditHours = 4,
                CreatedAtUtc = now
            }
        };

        context.Students.AddRange(students);
        context.Courses.AddRange(courses);

        // Save so PostgreSQL generates the primary keys needed below.
        await context.SaveChangesAsync(cancellationToken);

        // Enrollments: both students enrolled in all three courses (6 rows).
        var enrollments = new List<Enrollment>
        {
            new()
            {
                StudentId = students[0].Id,
                CourseId = courses[0].Id,
                Mark = 88,
                EnrolledAtUtc = now
            },
            new()
            {
                StudentId = students[0].Id,
                CourseId = courses[1].Id,
                Mark = 91,
                EnrolledAtUtc = now
            },
            new()
            {
                StudentId = students[0].Id,
                CourseId = courses[2].Id,
                Mark = null,
                EnrolledAtUtc = now
            },
            new()
            {
                StudentId = students[1].Id,
                CourseId = courses[0].Id,
                Mark = 95,
                EnrolledAtUtc = now
            },
            new()
            {
                StudentId = students[1].Id,
                CourseId = courses[1].Id,
                Mark = 84,
                EnrolledAtUtc = now
            },
            new()
            {
                StudentId = students[1].Id,
                CourseId = courses[2].Id,
                Mark = null,
                EnrolledAtUtc = now
            }
        };

        // Users: one teacher and one user per student.
        var passwordHasher = new PasswordHasher<User>();

        var teacher = new User
        {
            Username = "teacher1",
            Role = "Teacher",
            StudentId = null
        };

        var studentUser1 = new User
        {
            Username = "ali.hassan",
            Role = "Student",
            StudentId = students[0].Id
        };

        var studentUser2 = new User
        {
            Username = "sara.ahmed",
            Role = "Student",
            StudentId = students[1].Id
        };

        teacher.PasswordHash = passwordHasher.HashPassword(teacher, "Teacher@123");
        studentUser1.PasswordHash = passwordHasher.HashPassword(studentUser1, "Student@123");
        studentUser2.PasswordHash = passwordHasher.HashPassword(studentUser2, "Student@123");

        context.Enrollments.AddRange(enrollments);
        context.Users.AddRange(teacher, studentUser1, studentUser2);

        await context.SaveChangesAsync(cancellationToken);
    }
}
