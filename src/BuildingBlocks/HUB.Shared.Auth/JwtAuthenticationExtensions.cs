using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace HUB.Shared.Auth;

/// <summary>Registers JWT bearer auth that validates tokens issued by DASHBOARD (shared HMAC secret).</summary>
public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Adds authentication + authorization using the shared DASHBOARD JWT secret.
    /// Also wires SignalR's access_token query-string convention so WebSocket handshakes authenticate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">App configuration containing the <c>Jwt</c> section.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddHubJwtAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                      ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");

        if (string.IsNullOrWhiteSpace(options.Secret))
            throw new InvalidOperationException("Jwt:Secret is required and must match DASHBOARD.");

        services.AddSingleton(options);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret)),
                    ValidateIssuer           = true,
                    ValidIssuer              = options.Issuer,
                    ValidateAudience         = true,
                    ValidAudience            = options.Audience,
                    ValidateLifetime         = true,
                    ClockSkew                = TimeSpan.Zero,
                };

                // Allow SignalR WebSocket handshakes to carry the token via ?access_token=...
                opt.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path        = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization();
        return services;
    }

    /// <summary>Adds authentication + authorization middleware in the correct order.</summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder for chaining.</returns>
    public static IApplicationBuilder UseHubAuth(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
