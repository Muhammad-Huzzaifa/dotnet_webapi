using marks_api.Common;
using marks_api.Dtos.Enrollments;
using marks_api.Extensions;
using marks_api.Services.Interfaces;

namespace marks_api.Endpoints;

public static class EnrollmentEndpoints
{
    public static IEndpointRouteBuilder MapEnrollmentEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        // Enrollment management is Teacher-only.
        var courseEnrollments = endpoints
            .MapGroup("/api/courses/{courseId:int}/enrollments")
            .RequireAuthorization(policy => policy.RequireRole("Teacher"))
            .WithTags("Enrollments");

        courseEnrollments.MapGet("/", GetByCourseAsync)
            .WithName("GetCourseEnrollments");

        courseEnrollments.MapPost("/", CreateAsync)
            .WithName("CreateEnrollment");

        var enrollmentGroup = endpoints
            .MapGroup("/api/enrollments")
            .RequireAuthorization(policy => policy.RequireRole("Teacher"))
            .WithTags("Enrollments");

        enrollmentGroup.MapPut("/{id:int}/mark", UpdateMarkAsync)
            .WithName("UpdateEnrollmentMark");

        enrollmentGroup.MapDelete("/{id:int}", DeleteAsync)
            .WithName("DeleteEnrollment");

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        int courseId,
        CreateEnrollmentRequest request,
        IEnrollmentService enrollmentService,
        CancellationToken cancellationToken)
    {
        if (!request.TryValidate(out var validationErrors))
        {
            return Results.ValidationProblem(validationErrors);
        }

        var result = await enrollmentService.CreateAsync(
            courseId,
            request,
            cancellationToken);

        if (result.Status == ServiceStatus.Created &&
            result.Data is not null)
        {
            return Results.Created(
                $"/api/enrollments/{result.Data.Id}",
                result.Data);
        }

        return result.Status.ToProblemResult(result.Error);
    }

    private static async Task<IResult> GetByCourseAsync(
        int courseId,
        IEnrollmentService enrollmentService,
        CancellationToken cancellationToken)
    {
        var result = await enrollmentService.GetByCourseAsync(
            courseId,
            cancellationToken);

        return result.Status == ServiceStatus.Success
            ? Results.Ok(result.Data)
            : result.Status.ToProblemResult(result.Error);
    }

    private static async Task<IResult> UpdateMarkAsync(
        int id,
        UpdateMarkRequest request,
        IEnrollmentService enrollmentService,
        CancellationToken cancellationToken)
    {
        if (!request.TryValidate(out var validationErrors))
        {
            return Results.ValidationProblem(validationErrors);
        }

        var result = await enrollmentService.UpdateMarkAsync(
            id,
            request,
            cancellationToken);

        return result.Status == ServiceStatus.Success
            ? Results.Ok(result.Data)
            : result.Status.ToProblemResult(result.Error);
    }

    private static async Task<IResult> DeleteAsync(
        int id,
        IEnrollmentService enrollmentService,
        CancellationToken cancellationToken)
    {
        var result = await enrollmentService.DeleteAsync(
            id,
            cancellationToken);

        return result.Status == ServiceStatus.Success
            ? Results.NoContent()
            : result.Status.ToProblemResult(result.Error);
    }
}
