using FluentValidation;
using HUB.Notification.Application.Common.Exceptions;

namespace HUB.Notification.WebApi.Common;

/// <summary>Maps known exceptions to HTTP problem responses.</summary>
/// <param name="next">Next middleware.</param>
/// <param name="logger">Logger.</param>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    /// <summary>Invokes the pipeline, translating exceptions.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (ValidationException ex)
        {
            await Write(context, StatusCodes.Status400BadRequest, "validation_error",
                string.Join("; ", ex.Errors.Select(e => e.ErrorMessage)));
        }
        catch (NotFoundException ex) { await Write(context, StatusCodes.Status404NotFound, "not_found", ex.Message); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await Write(context, StatusCodes.Status500InternalServerError, "server_error", "An unexpected error occurred.");
        }
    }

    private static Task Write(HttpContext ctx, int status, string code, string message)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsJsonAsync(new { error = code, message });
    }
}
