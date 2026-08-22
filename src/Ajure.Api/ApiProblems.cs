using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Ajure.Api;

public static class ApiProblems
{
    public static IResult NotFound(HttpContext context, string code, string message) =>
        Create(context, StatusCodes.Status404NotFound, code, message, retryable: false);

    public static IResult Conflict(HttpContext context, string code, string message) =>
        Create(context, StatusCodes.Status409Conflict, code, message, retryable: false);

    public static IResult Validation(
        HttpContext context,
        string code,
        string message,
        object? details = null) =>
        Create(context, StatusCodes.Status400BadRequest, code, message, retryable: false, details);

    public static IResult Create(
        HttpContext context,
        int status,
        string code,
        string message,
        bool retryable,
        object? details = null) =>
        Results.Problem(
            statusCode: status,
            title: message,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["message"] = message,
                ["correlationId"] = context.TraceIdentifier,
                ["retryable"] = retryable,
                ["details"] = details
            });
}

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ApiLog.Unhandled(
            logger,
            httpContext.TraceIdentifier,
            exception.GetType().Name);
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "The request could not be completed."
        };
        problem.Extensions["code"] = "internal_error";
        problem.Extensions["message"] = problem.Title;
        problem.Extensions["correlationId"] = httpContext.TraceIdentifier;
        problem.Extensions["retryable"] = false;
        problem.Extensions["details"] = null;
        httpContext.Response.StatusCode = problem.Status.Value;
        await httpContext.Response
            .WriteAsJsonAsync(problem, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }
}

internal static partial class ApiLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Unhandled API error. CorrelationId={CorrelationId}, ExceptionType={ExceptionType}")]
    internal static partial void Unhandled(
        ILogger logger,
        string correlationId,
        string exceptionType);
}
