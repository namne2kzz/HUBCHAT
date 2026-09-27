using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace HUB.Chat.IntegrationTests.Infrastructure;

/// <summary>Mints JWTs the Chat API will accept, standing in for DASHBOARD's login endpoint.</summary>
/// <remarks>
/// HUB does not issue tokens — DASHBOARD does, and HUB validates them with a shared HMAC secret. There is
/// no login endpoint here to obtain a real one from, so tests sign their own with the same secret the
/// factory configures.
///
/// The claim names matter and are not arbitrary: <c>CurrentUser</c> reads <c>uid</c> first, then <c>sub</c>,
/// then <c>NameIdentifier</c>. A token carrying the id under some other name authenticates successfully and
/// then fails inside the handler, which is a confusing way for a test to break.
/// </remarks>
public static class TestTokens
{
    /// <summary>Creates a valid bearer token for a user.</summary>
    /// <param name="userId">The user id to put in the <c>uid</c> claim.</param>
    /// <param name="expires">Optional expiry; defaults to an hour out.</param>
    /// <returns>The encoded JWT.</returns>
    public static string For(Guid userId, DateTime? expires = null)
    {
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiry = expires ?? DateTime.UtcNow.AddHours(1);

        // notBefore has to sit in the past AND before the expiry: JwtSecurityToken refuses to construct
        // a token whose notBefore is later than its expires, which is what minting an already-expired
        // one runs into. Taking the earlier of "a minute ago" and the expiry satisfies both, and matters
        // because the API validates with ClockSkew = TimeSpan.Zero — a notBefore even slightly in the
        // future makes a perfectly good token come back 401.
        var notBefore = expiry < DateTime.UtcNow ? expiry.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(-1);

        var token = new JwtSecurityToken(
            issuer:             ApiFactory.JwtIssuer,
            audience:           ApiFactory.JwtAudience,
            claims:             [new Claim("uid", userId.ToString())],
            notBefore:          notBefore,
            expires:            expiry,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Creates a token signed with the wrong secret.</summary>
    /// <param name="userId">The user id to put in the <c>uid</c> claim.</param>
    /// <returns>An encoded JWT the API must reject.</returns>
    /// <remarks>
    /// Used to show that signature validation is genuinely on. A test that only ever sends valid tokens
    /// cannot tell a working validator from one that trusts anything well-formed.
    /// </remarks>
    public static string SignedWithTheWrongKey(Guid userId)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("a-different-key-that-is-also-at-least-32-bytes"));

        var token = new JwtSecurityToken(
            issuer:             ApiFactory.JwtIssuer,
            audience:           ApiFactory.JwtAudience,
            claims:             [new Claim("uid", userId.ToString())],
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Creates a token whose issuer the API does not accept.</summary>
    /// <param name="userId">The user id to put in the <c>uid</c> claim.</param>
    /// <returns>An encoded JWT the API must reject.</returns>
    public static string FromTheWrongIssuer(Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.JwtSecret));

        var token = new JwtSecurityToken(
            issuer:             "somebody-else",
            audience:           ApiFactory.JwtAudience,
            claims:             [new Claim("uid", userId.ToString())],
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
