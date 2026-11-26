using Claims.Application.Common;
using Claims.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Errors;

/// <summary>
/// Turns known exceptions into RFC 7807 responses. Since .NET 10 the exception handler middleware does not log
/// exceptions an IExceptionHandler has handled, so logging happens here.
/// </summary>
internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = ToProblem(exception);
        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception");
        }
        else
        {
            logger.LogInformation("Request refused with {StatusCode}: {Detail}", problem.Status, problem.Detail);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }

    internal static ProblemDetails ToProblem(Exception exception) => exception switch
    {
        DomainException => Problem(StatusCodes.Status409Conflict, "Business rule violated.", exception.Message),
        DbUpdateConcurrencyException => Problem(StatusCodes.Status409Conflict, "Concurrency conflict.",
            "The record was changed by someone else. Reload it and try again."),
        NotFoundException => Problem(StatusCodes.Status404NotFound, "Not found.", exception.Message),
        ForbiddenException => Problem(StatusCodes.Status403Forbidden, "Forbidden.", exception.Message),
        // Never echo an unexpected exception's message: it can contain internals.
        _ => Problem(StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null),
    };

    private static ProblemDetails Problem(int status, string title, string? detail) =>
        new() { Status = status, Title = title, Detail = detail };
}
