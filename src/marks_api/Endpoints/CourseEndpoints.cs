using marks_api.Common;
using marks_api.Dtos.Courses;
using marks_api.Extensions;
using marks_api.Services.Interfaces;

namespace marks_api.Endpoints;

public static class CourseEndpoints
{
    public static IEndpointRouteBuilder MapCourseEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/courses")
            .RequireAuthorization()
            .WithTags("Courses");

        // Read-only course access is available to both roles.
        group.MapGet("/", GetAllAsync)
            .WithName("GetCourses");

        group.MapGet("/{id:int}", GetByIdAsync)
            .WithName("GetCourseById");

        group.MapGet("/{id:int}/stats", GetStatsAsync)
            .WithName("GetCourseStats");

        // Course management is Teacher-only.
        group.MapPost("/", CreateAsync)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"))
            .WithName("CreateCourse");

        group.MapPut("/{id:int}", UpdateAsync)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"))
            .WithName("UpdateCourse");

        group.MapDelete("/{id:int}", DeleteAsync)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"))
            .WithName("DeleteCourse");

        return endpoints;
    }

    private static async Task<IResult> GetAllAsync(
        ICourseService courseService,
        CancellationToken cancellationToken)
    {
        var result = await courseService.GetAllAsync(cancellationToken);

        return result.Status == ServiceStatus.Success
            ? Results.Ok(result.Data)
            : result.Status.ToProblemResult(result.Error);
    }

    private static async Task<IResult> GetByIdAsync(
        int id,
        ICourseService courseService,
        CancellationToken cancellationToken)
    {
        var result = await courseService.GetByIdAsync(id, cancellationToken);

        return result.Status == ServiceStatus.Success
            ? Results.Ok(result.Data)
            : result.Status.ToProblemResult(result.Error);
    }

    private static async Task<IResult> GetStatsAsync(
        int id,
        ICourseService courseService,
        CancellationToken cancellationToken)
    {
        var result = await courseService.GetStatsAsync(id, cancellationToken);

        return result.Status == ServiceStatus.Success
            ? Results.Ok(result.Data)
            : result.Status.ToProblemResult(result.Error);
    }

    private static async Task<IResult> CreateAsync(
        CreateCourseRequest request,
        ICourseService courseService,
        CancellationToken cancellationToken)
    {
        if (!request.TryValidate(out var validationErrors))
        {
            return Results.ValidationProblem(validationErrors);
        }

        var result = await courseService.CreateAsync(
            request,
            cancellationToken);

        if (result.Status == ServiceStatus.Created &&
            result.Data is not null)
        {
            return Results.Created(
                $"/api/courses/{result.Data.Id}",
                result.Data);
        }

        return result.Status.ToProblemResult(result.Error);
    }

    private static async Task<IResult> UpdateAsync(
        int id,
        UpdateCourseRequest request,
        ICourseService courseService,
        CancellationToken cancellationToken)
    {
        if (!request.TryValidate(out var validationErrors))
        {
            return Results.ValidationProblem(validationErrors);
        }

        var result = await courseService.UpdateAsync(
            id,
            request,
            cancellationToken);

        return result.Status == ServiceStatus.Success
            ? Results.Ok(result.Data)
            : result.Status.ToProblemResult(result.Error);
    }

    private static async Task<IResult> DeleteAsync(
        int id,
        ICourseService courseService,
        CancellationToken cancellationToken)
    {
        var result = await courseService.DeleteAsync(
            id,
            cancellationToken);

        return result.Status == ServiceStatus.Success
            ? Results.NoContent()
            : result.Status.ToProblemResult(result.Error);
    }
}
