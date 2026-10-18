using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Exceptions;
using ApplicationValidationException = SupportDesk.Application.Exceptions.ValidationException;

namespace SupportDesk.Presentation.Middleware;

/// <summary>
/// Turns the application's exceptions into problem documents, so controllers never have to
/// catch anything. Add a new exception type here and every endpoint picks it up.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApplicationValidationException exception)
        {
            logger.LogInformation("Validation failed for {Path}.", context.Request.Path);

            var problem = new ValidationProblemDetails(
                exception.Errors.ToDictionary(pair => pair.Key, pair => pair.Value))
            {
                Title = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest
            };

            await WriteAsync(context, problem, StatusCodes.Status400BadRequest);
        }
        catch (NotFoundException exception)
        {
            await WriteAsync(context, Problem("Not Found", exception.Message, StatusCodes.Status404NotFound),
                StatusCodes.Status404NotFound);
        }
        catch (ConflictException exception)
        {
            await WriteAsync(context, Problem("Conflict", exception.Message, StatusCodes.Status409Conflict),
                StatusCodes.Status409Conflict);
        }
        catch (BusinessRuleViolationException exception)
        {
            await WriteAsync(context, Problem("Conflict", exception.Message, StatusCodes.Status409Conflict),
                StatusCodes.Status409Conflict);
        }
        catch (UnauthorizedException exception)
        {
            await WriteAsync(context, Problem("Unauthorized", exception.Message, StatusCodes.Status401Unauthorized),
                StatusCodes.Status401Unauthorized);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The caller went away; nothing to report.
            logger.LogDebug("Request {Path} was cancelled by the client.", context.Request.Path);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception while processing {Path}.", context.Request.Path);

            await WriteAsync(
                context,
                Problem(
                    "Internal Server Error",
                    "An unexpected error occurred. Please try again or contact support.",
                    StatusCodes.Status500InternalServerError),
                StatusCodes.Status500InternalServerError);
        }
    }

    private static ProblemDetails Problem(string title, string detail, int status) =>
        new() { Title = title, Detail = detail, Status = status };

    private static async Task WriteAsync(HttpContext context, ProblemDetails problem, int statusCode)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = MediaTypeNames.Application.Json;

        await context.Response.WriteAsJsonAsync(problem, problem.GetType());
    }
}
