using marks_api.Common;
using marks_api.Data;
using marks_api.Data.Entities;
using marks_api.Dtos.Students;
using marks_api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace marks_api.Services.Implementations;

public class StudentService : IStudentService
{
    private const int MaxPageSize = 100;

    // Documented default when TranscriptSettings:PassMark is not set.
    private const int DefaultPassMark = 50;

    private static readonly HashSet<string> AllowedSortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "id",
            "fullName",
            "registrationNo",
            "email",
            "createdAtUtc"
        };

    private readonly AppDbContext _context;
    private readonly ILogger<StudentService> _logger;
    private readonly int _passMark;

    public StudentService(
        AppDbContext context,
        IConfiguration configuration,
        ILogger<StudentService> logger)
    {
        _context = context;
        _logger = logger;

        var configuredPassMark =
            configuration.GetValue<int?>("TranscriptSettings:PassMark")
            ?? DefaultPassMark;

        if (configuredPassMark is < 0 or > 100)
        {
            _logger.LogWarning(
                "TranscriptSettings:PassMark {PassMark} is outside 0-100. " +
                "Falling back to {DefaultPassMark}.",
                configuredPassMark,
                DefaultPassMark);

            _passMark = DefaultPassMark;
        }
        else
        {
            _passMark = configuredPassMark;
        }
    }

    public async Task<ServiceResult<PagedResult<StudentResponse>>> GetAllAsync(
        StudentQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? 10;

        if (page < 1)
        {
            return ServiceResult<PagedResult<StudentResponse>>.BadRequest(
                "Page must be at least 1.");
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            return ServiceResult<PagedResult<StudentResponse>>.BadRequest(
                $"Page size must be between 1 and {MaxPageSize}.");
        }

        var sortBy = string.IsNullOrWhiteSpace(query.SortBy)
            ? "fullName"
            : query.SortBy.Trim();

        if (!AllowedSortFields.Contains(sortBy))
        {
            return ServiceResult<PagedResult<StudentResponse>>.BadRequest(
                "Invalid sortBy value. Allowed values: " +
                string.Join(", ", AllowedSortFields) + ".");
        }

        var sortOrder = string.IsNullOrWhiteSpace(query.SortOrder)
            ? "asc"
            : query.SortOrder.Trim().ToLowerInvariant();

        if (sortOrder is not ("asc" or "desc"))
        {
            return ServiceResult<PagedResult<StudentResponse>>.BadRequest(
                "Invalid sortOrder value. Use 'asc' or 'desc'.");
        }

        var skip = ((long)page - 1) * pageSize;

        if (skip > int.MaxValue)
        {
            return ServiceResult<PagedResult<StudentResponse>>.BadRequest(
                "Page is too large.");
        }

        var studentsQuery = _context.Students.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Escape LIKE metacharacters so user input is matched literally.
            var term = query.Search.Trim()
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");

            var pattern = $"%{term}%";

            studentsQuery = studentsQuery.Where(s =>
                EF.Functions.ILike(s.FullName, pattern, "\\") ||
                EF.Functions.ILike(s.Email, pattern, "\\") ||
                EF.Functions.ILike(s.RegistrationNo, pattern, "\\"));
        }

        var totalRecords = await studentsQuery.CountAsync(cancellationToken);

        var orderedQuery = (sortBy.ToLowerInvariant(), sortOrder) switch
        {
            ("id", "desc") => studentsQuery.OrderByDescending(s => s.Id),
            ("id", _) => studentsQuery.OrderBy(s => s.Id),
            ("email", "desc") => studentsQuery.OrderByDescending(s => s.Email),
            ("email", _) => studentsQuery.OrderBy(s => s.Email),
            ("registrationno", "desc") => studentsQuery
                .OrderByDescending(s => s.RegistrationNo),
            ("registrationno", _) => studentsQuery
                .OrderBy(s => s.RegistrationNo),
            ("createdatutc", "desc") => studentsQuery
                .OrderByDescending(s => s.CreatedAtUtc),
            ("createdatutc", _) => studentsQuery
                .OrderBy(s => s.CreatedAtUtc),
            ("fullname", "desc") => studentsQuery
                .OrderByDescending(s => s.FullName),
            _ => studentsQuery.OrderBy(s => s.FullName)
        };

        var students = await orderedQuery
            .ThenBy(s => s.Id)
            .Skip((int)skip)
            .Take(pageSize)
            .Select(s => new StudentResponse
            {
                Id = s.Id,
                RegistrationNo = s.RegistrationNo,
                FullName = s.FullName,
                Email = s.Email,
                CreatedAtUtc = s.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(
            totalRecords / (double)pageSize);

        var result = new PagedResult<StudentResponse>(
            students,
            page,
            pageSize,
            totalRecords,
            totalPages);

        return ServiceResult<PagedResult<StudentResponse>>.Success(result);
    }

    public async Task<ServiceResult<StudentResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var student = await _context.Students
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new StudentResponse
            {
                Id = s.Id,
                RegistrationNo = s.RegistrationNo,
                FullName = s.FullName,
                Email = s.Email,
                CreatedAtUtc = s.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        return student is null
            ? ServiceResult<StudentResponse>.NotFound("Student not found.")
            : ServiceResult<StudentResponse>.Success(student);
    }

    public async Task<ServiceResult<StudentResponse>> CreateAsync(
        CreateStudentRequest request,
        CancellationToken cancellationToken = default)
    {
        var registrationNo = request.RegistrationNo.Trim();
        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(registrationNo) ||
            string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(email))
        {
            return ServiceResult<StudentResponse>.BadRequest(
                "Registration number, full name, and email are required.");
        }

        var registrationExists = await _context.Students
            .AnyAsync(
                s => s.RegistrationNo == registrationNo,
                cancellationToken);

        if (registrationExists)
        {
            return ServiceResult<StudentResponse>.Conflict(
                "A student with this registration number already exists.");
        }

        var emailExists = await _context.Students
            .AnyAsync(s => s.Email == email, cancellationToken);

        if (emailExists)
        {
            return ServiceResult<StudentResponse>.Conflict(
                "A student with this email already exists.");
        }

        var student = new Student
        {
            RegistrationNo = registrationNo,
            FullName = fullName,
            Email = email,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Students.Add(student);

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
            // The unique indexes caught a concurrent duplicate.
            _context.Entry(student).State = EntityState.Detached;

            return ServiceResult<StudentResponse>.Conflict(
                "A student with this registration number or email already exists.");
        }

        _logger.LogInformation(
            "Created student {StudentId} ({RegistrationNo}).",
            student.Id,
            student.RegistrationNo);

        return ServiceResult<StudentResponse>.Created(ToResponse(student));
    }

    public async Task<ServiceResult<StudentResponse>> UpdateAsync(
        int id,
        UpdateStudentRequest request,
        CancellationToken cancellationToken = default)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            return ServiceResult<StudentResponse>.NotFound(
                "Student not found.");
        }

        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(email))
        {
            return ServiceResult<StudentResponse>.BadRequest(
                "Full name and email are required.");
        }

        var emailExists = await _context.Students
            .AnyAsync(
                s => s.Email == email && s.Id != id,
                cancellationToken);

        if (emailExists)
        {
            return ServiceResult<StudentResponse>.Conflict(
                "Another student already uses this email.");
        }

        student.FullName = fullName;
        student.Email = email;

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
            // The unique email index caught a concurrent duplicate.
            _context.Entry(student).State = EntityState.Detached;

            return ServiceResult<StudentResponse>.Conflict(
                "Another student already uses this email.");
        }

        _logger.LogInformation(
            "Updated student {StudentId} ({RegistrationNo}).",
            student.Id,
            student.RegistrationNo);

        return ServiceResult<StudentResponse>.Success(ToResponse(student));
    }

    public async Task<ServiceResult<StudentTranscriptResponse>> GetTranscriptAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var student = await _context.Students
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new
            {
                s.Id,
                s.RegistrationNo,
                s.FullName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            return ServiceResult<StudentTranscriptResponse>.NotFound(
                "Student not found.");
        }

        var enrollments = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == id)
            .OrderBy(e => e.Course.Code)
            .Select(e => new TranscriptCourseResponse
            {
                CourseCode = e.Course.Code,
                CourseTitle = e.Course.Title,
                CreditHours = e.Course.CreditHours,
                Mark = e.Mark
            })
            .ToListAsync(cancellationToken);

        // Ungraded courses stay in the transcript but do not contribute
        // to the weighted average.
        var gradedCourses = enrollments
            .Where(e => e.Mark.HasValue)
            .ToList();

        var gradedCredits = gradedCourses.Sum(e => e.CreditHours);

        double? weightedAverage = gradedCredits == 0
            ? null
            : Math.Round(
                gradedCourses.Sum(
                    e => (double)e.Mark!.Value * e.CreditHours) /
                gradedCredits,
                2,
                MidpointRounding.AwayFromZero);

        var transcript = new StudentTranscriptResponse
        {
            StudentId = student.Id,
            RegistrationNo = student.RegistrationNo,
            FullName = student.FullName,
            Enrollments = enrollments,
            WeightedAverage = weightedAverage,
            PassMark = _passMark,
            IsPassed = weightedAverage is double average
                ? average >= _passMark
                : null
        };

        return ServiceResult<StudentTranscriptResponse>.Success(transcript);
    }

    public async Task<ServiceResult> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            return ServiceResult.NotFound("Student not found.");
        }

        var hasEnrollments = await _context.Enrollments
            .AnyAsync(e => e.StudentId == id, cancellationToken);

        if (hasEnrollments)
        {
            return ServiceResult.Conflict(
                "Student has existing enrollments and cannot be deleted.");
        }

        var hasUserAccount = await _context.Users
            .AnyAsync(u => u.StudentId == id, cancellationToken);

        if (hasUserAccount)
        {
            return ServiceResult.Conflict(
                "Student is linked to a user account and cannot be deleted.");
        }

        _context.Students.Remove(student);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted student {StudentId} ({RegistrationNo}).",
            student.Id,
            student.RegistrationNo);

        return ServiceResult.Success();
    }

    private static StudentResponse ToResponse(Student student)
        => new()
        {
            Id = student.Id,
            RegistrationNo = student.RegistrationNo,
            FullName = student.FullName,
            Email = student.Email,
            CreatedAtUtc = student.CreatedAtUtc
        };
}
