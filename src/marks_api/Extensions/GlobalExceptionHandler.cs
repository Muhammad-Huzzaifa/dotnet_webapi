using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace marks_api.Extensions;

/// <summary>
/// Converts unhandled exceptions into a generic ProblemDetails response.
/// The exception itself is written to the log, never to the response body.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Unhandled exception while processing {Method} {Path}. TraceId: {TraceId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.TraceIdentifier);

        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var statusCode = exception is BadHttpRequestException
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status400BadRequest
                ? "Bad Request"
                : "Internal Server Error",
            Detail = statusCode == StatusCodes.Status400BadRequest
                ? "The request could not be parsed."
                : "An unexpected error occurred while processing the request.",
            Instance = httpContext.Request.Path,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        await Results.Problem(problem).ExecuteAsync(httpContext);

        return true;
    }
}
