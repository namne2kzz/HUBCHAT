namespace HUB.DashboardGateway.Dashboard;

/// <summary>Minimal user profile returned by DASHBOARD /internal/v1/users/{id}. Avatar is a CSS class, not a URL.</summary>
public sealed record UserProfile(
    Guid Id,
    string Name,
    string Email,
    string AvatarClass,
    bool IsGlobalAdmin,
    bool IsDeleted);
