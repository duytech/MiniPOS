using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Pos.Application.Common;
using Pos.Domain.Common;

namespace Pos.Api.Errors;

/// <summary>
/// Maps exceptions to ProblemDetails. Anything not listed here is a bug: it returns a 500 with the exception details so the
/// failure can be diagnosed from the response.
/// </summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException ex => new ValidationProblemDetails(ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
            },
            NotFoundException ex => Problem(StatusCodes.Status404NotFound, "Not found", ex.Message),
            ConflictException ex => Problem(StatusCodes.Status409Conflict, "Conflict", ex.Message),
            DomainException ex => Problem(StatusCodes.Status422UnprocessableEntity, "Business rule violated", ex.Message),
            _ => Problem(StatusCodes.Status500InternalServerError, "Unexpected error", exception.ToString()),
        };

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };
}
