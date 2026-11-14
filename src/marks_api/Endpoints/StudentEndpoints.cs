using System.Security.Claims;
using marks_api.Common;
using marks_api.Dtos.Students;
using marks_api.Extensions;
using marks_api.Services.Interfaces;

namespace marks_api.Endpoints;

public static class StudentEndpoints
{
    public static IEndpointRouteBuilder MapStudentEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/students")
            .RequireAuthorization()
            .WithTags("Students");

        group.MapGet("/", GetAllAsync)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"))
            .WithName("GetStudents");

        group.MapGet("/{id:int}", GetByIdAsync)
            .WithName("GetStudentById");

        group.MapGet("/{id:int}/transcript", GetTranscriptAsync)
            .WithName("GetStudentTranscript");

        group.MapPost("/", CreateAsync)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"))
            .WithName("CreateStudent");

        group.MapPut("/{id:int}", UpdateAsync)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"))
            .WithName("UpdateStudent");

        group.MapDelete("/{id:int}", DeleteAsync)
            .RequireAuthorization(policy => policy.RequireRole("Teacher"))
            .WithName("DeleteStudent");

        return endpoints;
    }

    private static async Task<IResult> GetAllAsync(
        [AsParameters] StudentQueryParameters query,
        IStudentService studentService,
        CancellationToken cancellationToken)
    {
        var result = await studentService.GetAllAsync(
            query,
            cancellationToken);

        return result.Status == ServiceStatus.Success
            ? Results.Ok(result.Data)
            : result.Status.ToProblemResult(result.Error);
    }

    private static async Task<IResult> GetByIdAsync(
        int id,
        ClaimsPrincipal user,
        IStudentService studentService,
        CancellationToken cancellationToken)
    {
        // A Student may read only their own record.
        if (!user.CanAccessStudent(id))
        {
            return Results.Forbid();
        }

        var result = await studentService.GetByIdAsync(
            id,
            cancellationToken);

        return result.Status == ServiceStatus.Success
            ? Results.Ok(result.Data)
            : result.Status.ToProblemResult(result.Error);
    }

    private static async Task<IResult> GetTranscriptAsync(
        int id,
        ClaimsPrincipal user,
        IStudentService studentService,
        CancellationToken cancellationToken)
    {
        // A Student may read only their own transcript.
        if (!user.CanAccessStudent(id))
        {
            return Results.Forbid();
        }

        var result = await studentService.GetTranscriptAsync(
            id,
            cancellationToken);

        return result.Status == ServiceStatus.Success
            ? Results.Ok(result.Data)
            : result.Status.ToProblemResult(result.Error);
    }

    private static async Task<IResult> CreateAsync(
        CreateStudentRequest request,
        IStudentService studentService,
        CancellationToken cancellationToken)
    {
        if (!request.TryValidate(out var validationErrors))
        {
            return Results.ValidationProblem(validationErrors);
        }

        var result = await studentService.CreateAsync(
            request,
            cancellationToken);

        if (result.Status == ServiceStatus.Created &&
            result.Data is not null)
        {
            return Results.Created(
                $"/api/students/{result.Data.Id}",
                result.Data);
        }

        return result.Status.ToProblemResult(result.Error);
    }

    private static async Task<IResult> UpdateAsync(
        int id,
        UpdateStudentRequest request,
        IStudentService studentService,
        CancellationToken cancellationToken)
    {
        if (!request.TryValidate(out var validationErrors))
        {
            return Results.ValidationProblem(validationErrors);
        }

        var result = await studentService.UpdateAsync(
            id,
            request,
            cancellationToken);

        return result.Status == ServiceStatus.Success
            ? Results.Ok(result.Data)
            : result.Status.ToProblemResult(result.Error);
    }

    private static async Task<IResult> DeleteAsync(
        int id,
        IStudentService studentService,
        CancellationToken cancellationToken)
    {
        var result = await studentService.DeleteAsync(
            id,
            cancellationToken);

        return result.Status == ServiceStatus.Success
            ? Results.NoContent()
            : result.Status.ToProblemResult(result.Error);
    }
}
