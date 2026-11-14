using marks_api.Common;
using marks_api.Data;
using marks_api.Data.Entities;
using marks_api.Dtos.Courses;
using marks_api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace marks_api.Services.Implementations;

public class CourseService : ICourseService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CourseService> _logger;

    public CourseService(
        AppDbContext context,
        ILogger<CourseService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ServiceResult<IReadOnlyList<CourseResponse>>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var courses = await _context.Courses
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new CourseResponse
            {
                Id = c.Id,
                Code = c.Code,
                Title = c.Title,
                CreditHours = c.CreditHours,
                CreatedAtUtc = c.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<CourseResponse>>.Success(courses);
    }

    public async Task<ServiceResult<CourseResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CourseResponse
            {
                Id = c.Id,
                Code = c.Code,
                Title = c.Title,
                CreditHours = c.CreditHours,
                CreatedAtUtc = c.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        return course is null
            ? ServiceResult<CourseResponse>.NotFound("Course not found.")
            : ServiceResult<CourseResponse>.Success(course);
    }

    public async Task<ServiceResult<CourseStatsResponse>> GetStatsAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Code,
                c.Title
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (course is null)
        {
            return ServiceResult<CourseStatsResponse>.NotFound(
                "Course not found.");
        }

        // Only the mark column is needed; NULL marks are kept so the
        // graded/ungraded split can be calculated accurately.
        var marks = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == id)
            .Select(e => e.Mark)
            .ToListAsync(cancellationToken);

        var gradedMarks = marks
            .Where(mark => mark.HasValue)
            .Select(mark => mark!.Value)
            .ToList();

        var stats = new CourseStatsResponse
        {
            CourseId = course.Id,
            CourseCode = course.Code,
            CourseTitle = course.Title,
            TotalEnrolled = marks.Count,
            GradedCount = gradedMarks.Count,
            UngradedCount = marks.Count - gradedMarks.Count,
            AverageMark = gradedMarks.Count == 0
                ? null
                : Math.Round(
                    gradedMarks.Average(),
                    2,
                    MidpointRounding.AwayFromZero),
            HighestMark = gradedMarks.Count == 0
                ? null
                : gradedMarks.Max(),
            LowestMark = gradedMarks.Count == 0
                ? null
                : gradedMarks.Min()
        };

        return ServiceResult<CourseStatsResponse>.Success(stats);
    }

    public async Task<ServiceResult<CourseResponse>> CreateAsync(
        CreateCourseRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var title = request.Title.Trim();

        if (string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(title))
        {
            return ServiceResult<CourseResponse>.BadRequest(
                "Course code and title are required.");
        }

        if (request.CreditHours is < 1 or > 4)
        {
            return ServiceResult<CourseResponse>.BadRequest(
                "Credit hours must be between 1 and 4.");
        }

        var codeExists = await _context.Courses
            .AnyAsync(c => c.Code == code, cancellationToken);

        if (codeExists)
        {
            return ServiceResult<CourseResponse>.Conflict(
                "A course with this course code already exists.");
        }

        var course = new Course
        {
            Code = code,
            Title = title,
            CreditHours = request.CreditHours,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Courses.Add(course);

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
            // The unique index caught a concurrent duplicate.
            _context.Entry(course).State = EntityState.Detached;

            return ServiceResult<CourseResponse>.Conflict(
                "A course with this course code already exists.");
        }

        _logger.LogInformation(
            "Created course {CourseId} ({Code}).",
            course.Id,
            course.Code);

        return ServiceResult<CourseResponse>.Created(ToResponse(course));
    }

    public async Task<ServiceResult<CourseResponse>> UpdateAsync(
        int id,
        UpdateCourseRequest request,
        CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (course is null)
        {
            return ServiceResult<CourseResponse>.NotFound(
                "Course not found.");
        }

        var title = request.Title.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            return ServiceResult<CourseResponse>.BadRequest(
                "Course title is required.");
        }

        if (request.CreditHours is < 1 or > 4)
        {
            return ServiceResult<CourseResponse>.BadRequest(
                "Credit hours must be between 1 and 4.");
        }

        course.Title = title;
        course.CreditHours = request.CreditHours;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated course {CourseId} ({Code}).",
            course.Id,
            course.Code);

        return ServiceResult<CourseResponse>.Success(ToResponse(course));
    }

    public async Task<ServiceResult> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (course is null)
        {
            return ServiceResult.NotFound("Course not found.");
        }

        var hasEnrollments = await _context.Enrollments
            .AnyAsync(e => e.CourseId == id, cancellationToken);

        if (hasEnrollments)
        {
            return ServiceResult.Conflict(
                "Course has existing enrollments and cannot be deleted.");
        }

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted course {CourseId} ({Code}).",
            course.Id,
            course.Code);

        return ServiceResult.Success();
    }

    private static CourseResponse ToResponse(Course course)
        => new()
        {
            Id = course.Id,
            Code = course.Code,
            Title = course.Title,
            CreditHours = course.CreditHours,
            CreatedAtUtc = course.CreatedAtUtc
        };
}
