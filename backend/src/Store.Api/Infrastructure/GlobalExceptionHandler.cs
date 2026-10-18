using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Application.Abstractions;
using Store.Application.Common;
using Store.Domain.Exceptions;

namespace Store.Api.Infrastructure;

/// <summary>Turns every unhandled exception into an RFC 7807 ProblemDetails response.</summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetails,
        IDbExceptionClassifier dbErrors,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _dbErrors = dbErrors;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // Client went away - nothing useful to send back.
            _logger.LogInformation("Request {Path} was cancelled by the client", httpContext.Request.Path);
            return true;
        }

        var problem = exception switch
        {
            ValidationException ex => Problem(ex.StatusCode, ex.Title, ex.Message, ex.Errors.Count > 0 ? ex.Errors : null),
            ConflictException ex => Problem(ex.StatusCode, ex.Title, ex.Message, code: ex.Code),
            AppException ex => Problem(ex.StatusCode, ex.Title, ex.Message),
            DomainException ex => Problem(StatusCodes.Status400BadRequest, "Business rule violated", ex.Message),
            DbUpdateConcurrencyException => Problem(StatusCodes.Status409Conflict, "Conflict",
                "The data was changed by another request. Reload and try again.", code: "concurrency"),
            _ when _dbErrors.IsDeadlock(exception) => Problem(StatusCodes.Status409Conflict, "Conflict",
                "The request clashed with another update. Please try again.", code: "concurrency"),
            BadHttpRequestException ex => Problem(ex.StatusCode, "Bad request", ex.Message),
            _ => Problem(StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred.")
        };

        if (problem.Status >= 500)
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        else
            _logger.LogWarning("Request {Method} {Path} failed with {StatusCode}: {Message}",
                httpContext.Request.Method, httpContext.Request.Path, problem.Status, exception.Message);

        problem.Instance = httpContext.Request.Path;
        httpContext.Response.StatusCode = problem.Status!.Value;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    private static ProblemDetails Problem(int status, string title, string detail,
        IDictionary<string, string[]>? errors = null, string? code = null)
    {
        var problem = errors is null
            ? new ProblemDetails()
            : new ValidationProblemDetails(errors);

        problem.Status = status;
        problem.Title = title;
        problem.Detail = detail;

        if (code is not null)
            problem.Extensions["code"] = code;

        return problem;
    }
}
