using Microsoft.AspNetCore.Mvc;

namespace marks_api.Extensions;

public static class ProblemDetailsExtensions
{
    public static ProblemDetails CreateProblem(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        return problem;
    }
}
