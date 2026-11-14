using marks_api.Common;

namespace marks_api.Extensions;

public static class EndpointResultExtensions
{
    public static IResult ToProblemResult(
        this ServiceStatus status,
        string? error)
    {
        return status switch
        {
            ServiceStatus.NotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: error),
            ServiceStatus.Conflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: error),
            ServiceStatus.BadRequest => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: error),
            _ => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: "An unexpected error occurred.")
        };
    }
}
