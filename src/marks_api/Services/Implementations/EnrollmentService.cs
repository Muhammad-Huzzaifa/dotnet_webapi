using marks_api.Common;
using marks_api.Data;
using marks_api.Data.Entities;
using marks_api.Dtos.Enrollments;
using marks_api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace marks_api.Services.Implementations;

public class EnrollmentService : IEnrollmentService
{
    private readonly AppDbContext _context;
    private readonly ILogger<EnrollmentService> _logger;

    public EnrollmentService(
        AppDbContext context,
        ILogger<EnrollmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ServiceResult<EnrollmentResponse>> CreateAsync(
        int courseId,
        CreateEnrollmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Where(c => c.Id == courseId)
            .Select(c => new
            {
                c.Id,
                c.Code,
                c.Title
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (course is null)
        {
            return ServiceResult<EnrollmentResponse>.NotFound(
                "Course not found.");
        }

        var student = await _context.Students
            .AsNoTracking()
            .Where(s => s.Id == request.StudentId)
            .Select(s => new
            {
                s.Id,
                s.FullName,
                s.RegistrationNo
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            return ServiceResult<EnrollmentResponse>.NotFound(
                "Student not found.");
        }

        if (request.Mark is < 0 or > 100)
        {
            return ServiceResult<EnrollmentResponse>.BadRequest(
                "Mark must be between 0 and 100 when provided.");
        }

        var alreadyEnrolled = await _context.Enrollments
            .AnyAsync(
                e => e.StudentId == request.StudentId &&
                     e.CourseId == courseId,
                cancellationToken);

        if (alreadyEnrolled)
        {
            return ServiceResult<EnrollmentResponse>.Conflict(
                "Student is already enrolled in this course.");
        }

        var enrollment = new Enrollment
        {
            StudentId = request.StudentId,
            CourseId = courseId,
            Mark = request.Mark,
            EnrolledAtUtc = DateTime.UtcNow
        };

        _context.Enrollments.Add(enrollment);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException
            {
                SqlState: "23505"
            })
        {
            // The composite unique index caught a concurrent duplicate.
            _context.Entry(enrollment).State = EntityState.Detached;

            return ServiceResult<EnrollmentResponse>.Conflict(
                "Student is already enrolled in this course.");
        }

        _logger.LogInformation(
            "Created enrollment {EnrollmentId}: student {StudentId} -> course {CourseId}.",
            enrollment.Id,
            enrollment.StudentId,
            enrollment.CourseId);

        var response = new EnrollmentResponse
        {
            Id = enrollment.Id,
            StudentId = student.Id,
            StudentName = student.FullName,
            RegistrationNo = student.RegistrationNo,
            CourseId = course.Id,
            CourseCode = course.Code,
            CourseTitle = course.Title,
            Mark = enrollment.Mark,
            EnrolledAtUtc = enrollment.EnrolledAtUtc
        };

        return ServiceResult<EnrollmentResponse>.Created(response);
    }

    public async Task<ServiceResult<IReadOnlyList<EnrollmentResponse>>>
        GetByCourseAsync(
            int courseId,
            CancellationToken cancellationToken = default)
    {
        var courseExists = await _context.Courses
            .AsNoTracking()
            .AnyAsync(c => c.Id == courseId, cancellationToken);

        if (!courseExists)
        {
            return ServiceResult<IReadOnlyList<EnrollmentResponse>>
                .NotFound("Course not found.");
        }

        var enrollments = await ProjectEnrollment(
                _context.Enrollments
                    .AsNoTracking()
                    .Where(e => e.CourseId == courseId)
                    .OrderBy(e => e.Student.FullName))
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<EnrollmentResponse>>
            .Success(enrollments);
    }

    public async Task<ServiceResult<EnrollmentResponse>> UpdateMarkAsync(
        int id,
        UpdateMarkRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Mark is < 0 or > 100)
        {
            return ServiceResult<EnrollmentResponse>.BadRequest(
                "Mark must be between 0 and 100 when provided.");
        }

        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (enrollment is null)
        {
            return ServiceResult<EnrollmentResponse>.NotFound(
                "Enrollment not found.");
        }

        enrollment.Mark = request.Mark;

        await _context.SaveChangesAsync(cancellationToken);

        var updated = await ProjectEnrollment(
                _context.Enrollments
                    .AsNoTracking()
                    .Where(e => e.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

        if (updated is null)
        {
            return ServiceResult<EnrollmentResponse>.NotFound(
                "Enrollment not found.");
        }

        _logger.LogInformation(
            "Updated mark for enrollment {EnrollmentId}.",
            id);

        return ServiceResult<EnrollmentResponse>.Success(updated);
    }

    public async Task<ServiceResult> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (enrollment is null)
        {
            return ServiceResult.NotFound("Enrollment not found.");
        }

        _context.Enrollments.Remove(enrollment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted enrollment {EnrollmentId}.",
            id);

        return ServiceResult.Success();
    }

    private static IQueryable<EnrollmentResponse> ProjectEnrollment(
        IQueryable<Enrollment> query)
    {
        return query.Select(e => new EnrollmentResponse
        {
            Id = e.Id,
            StudentId = e.StudentId,
            StudentName = e.Student.FullName,
            RegistrationNo = e.Student.RegistrationNo,
            CourseId = e.CourseId,
            CourseCode = e.Course.Code,
            CourseTitle = e.Course.Title,
            Mark = e.Mark,
            EnrolledAtUtc = e.EnrolledAtUtc
        });
    }
}
