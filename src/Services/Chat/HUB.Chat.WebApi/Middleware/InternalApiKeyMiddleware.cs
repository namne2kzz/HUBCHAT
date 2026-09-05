namespace HUB.Chat.WebApi.Middleware;

/// <summary>
/// Guards <c>/internal/*</c> routes with a shared static token (header <c>X-Internal-Token</c>).
/// Only DASHBOARD backend (running on the same Docker network) should know this secret.
/// </summary>
/// <param name="next">Next middleware delegate.</param>
/// <param name="config">Application configuration.</param>
public sealed class InternalApiKeyMiddleware(RequestDelegate next, IConfiguration config)
{
    private const string Header = "X-Internal-Token";

    /// <summary>Validates the internal token before forwarding the request.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/internal"))
        {
            var expected = config["InternalApi:Token"];
            if (string.IsNullOrWhiteSpace(expected))
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync("InternalApi:Token not configured.");
                return;
            }

            if (!context.Request.Headers.TryGetValue(Header, out var provided) ||
                !string.Equals(provided, expected, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid or missing X-Internal-Token.");
                return;
            }
        }

        await next(context);
    }
}
