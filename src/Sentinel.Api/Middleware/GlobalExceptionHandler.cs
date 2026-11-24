using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sentinel.Application.Exceptions;
using Sentinel.Domain.Exceptions;

namespace Sentinel.Api.Middleware;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            WorkflowNotFoundException or ScenarioNotFoundException =>
                (StatusCodes.Status404NotFound, "Resource Not Found"),
            DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, "Concurrency Conflict"),
            DomainException =>
                (StatusCodes.Status400BadRequest, "Domain Validation Error"),
            _ => (0, string.Empty)
        };

        if (statusCode == 0)
        {
            return false;
        }

        _logger.LogWarning(exception, "Handled {ExceptionType} with HTTP {StatusCode}.", exception.GetType().Name, statusCode);

        httpContext.Response.StatusCode = statusCode;

        var detail = exception is DbUpdateConcurrencyException
            ? "The workflow was modified concurrently by another operation. Refresh and retry."
            : exception.Message;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            }
        });
    }
}
