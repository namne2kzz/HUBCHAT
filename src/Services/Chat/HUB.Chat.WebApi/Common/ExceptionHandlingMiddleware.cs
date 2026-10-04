using FluentValidation;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Domain.Common;

namespace HUB.Chat.WebApi.Common;

/// <summary>Translates domain/application exceptions into HTTP problem responses.</summary>
/// <param name="next">Next middleware.</param>
/// <param name="logger">Logger.</param>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    /// <summary>Invokes the pipeline, mapping known exceptions to status codes.</summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, "validation_error",
                string.Join("; ", ex.Errors.Select(e => e.ErrorMessage)));
        }
        catch (NotFoundException ex)
        {
            await WriteAsync(context, StatusCodes.Status404NotFound, "not_found", ex.Message);
        }
        catch (ForbiddenException ex)
        {
            await WriteAsync(context, StatusCodes.Status403Forbidden, "forbidden", ex.Message);
        }
        catch (DomainException ex)
        {
            await WriteAsync(context, StatusCodes.Status409Conflict, "domain_error", ex.Message);
        }
        catch (UniqueConstraintViolationException ex)
        {
            // A duplicate the handler did not resolve (e.g. a slug clash) — a conflict, not a server fault.
            logger.LogWarning(ex, "Unique constraint {Constraint} violated", ex.ConstraintName);
            await WriteAsync(context, StatusCodes.Status409Conflict, "conflict", "A resource with the same identity already exists.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteAsync(context, StatusCodes.Status500InternalServerError, "server_error", "An unexpected error occurred.");
        }
    }

    private static Task WriteAsync(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(new { error = code, message });
    }
}
