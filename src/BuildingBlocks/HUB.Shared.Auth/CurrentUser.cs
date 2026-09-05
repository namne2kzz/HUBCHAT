using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace HUB.Shared.Auth;

/// <summary>Resolves the current user from the JWT claims on the active HTTP context.</summary>
/// <param name="accessor">Accessor for the current <see cref="HttpContext"/>.</param>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    // DASHBOARD stores the user id in both `sub` and a custom `uid` claim.
    private const string UidClaim = "uid";

    /// <inheritdoc />
    public bool IsAuthenticated => IdOrNull is not null;

    /// <inheritdoc />
    public Guid Id => IdOrNull ?? throw new InvalidOperationException("No authenticated user on the current request.");

    /// <inheritdoc />
    public Guid? IdOrNull
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true) return null;

            var raw = principal.FindFirstValue(UidClaim)
                      ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }
}
