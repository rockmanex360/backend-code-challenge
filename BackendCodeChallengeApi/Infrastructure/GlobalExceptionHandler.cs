using BackendCodeChallengeApi.Data.Errors;
using Microsoft.AspNetCore.Diagnostics;

namespace BackendCodeChallengeApi.Infrastructure;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var request = httpContext.Request;

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request {Method} {Path} cancelled by client", request.Method, request.Path);
            httpContext.Response.StatusCode = 499;
            return true;
        }

        var status = exception switch
        {
            BadRequestException => StatusCodes.Status400BadRequest,
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        if (exception is DomainException)
            logger.LogWarning("{ErrorType} on {Method} {Path}: {Message}",
                exception.GetType().Name, request.Method, request.Path, exception.Message);
        else
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", request.Method, request.Path);

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = status,
                Detail = exception is DomainException ? exception.Message : "An unexpected error occurred."
            }
        });
    }
}