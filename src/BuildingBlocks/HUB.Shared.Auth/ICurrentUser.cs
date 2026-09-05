namespace HUB.Shared.Auth;

/// <summary>Ambient accessor for the authenticated user, resolved from the validated JWT.</summary>
public interface ICurrentUser
{
    /// <summary>The user id (from the <c>sub</c>/<c>uid</c> claim). Throws if unauthenticated.</summary>
    Guid Id { get; }

    /// <summary>True when a valid authenticated user is present on the current request.</summary>
    bool IsAuthenticated { get; }

    /// <summary>The user id if authenticated; otherwise null.</summary>
    Guid? IdOrNull { get; }
}
