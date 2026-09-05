namespace HUB.Shared.Auth;

/// <summary>Strongly-typed JWT validation settings. MUST mirror DASHBOARD's JwtSettings exactly.</summary>
public sealed class JwtOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Shared HMAC-SHA256 signing secret — identical to DASHBOARD's JwtSettings:Secret.</summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>Expected token issuer.</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Expected token audience.</summary>
    public string Audience { get; init; } = string.Empty;
}
