using HUB.DashboardGateway.Directory;
using HUB.Shared.Auth;

namespace HUB.DashboardGateway.Endpoints;

/// <summary>Minimal-API endpoints exposing cached DASHBOARD directory data to authenticated HUB users.</summary>
public static class DirectoryEndpoints
{
    /// <summary>Caps the batch profile lookup so one request cannot ask for an unbounded id list.</summary>
    private const int MaxBatchUserIds = 100;

    /// <summary>Maps the /api/v1/directory routes.</summary>
    /// <param name="app">The web application.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapDirectoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/directory").RequireAuthorization();

        // Profile of any user (for rendering author/mention chips).
        group.MapGet("/users/{id:guid}", async (Guid id, IDirectoryService directory, CancellationToken ct) =>
        {
            var user = await directory.GetUserAsync(id, ct);
            return user is null ? Results.NotFound() : Results.Ok(user);
        });

        // Several profiles in one round-trip — for rendering a list of authors or mention chips without
        // one request per user. Served per-user from cache, so only the ids not already cached are
        // fetched from DASHBOARD.
        group.MapGet("/users", async (string ids, IDirectoryService directory, CancellationToken ct) =>
        {
            var parsed = ids.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
                            .Where(g => g.HasValue)
                            .Select(g => g!.Value)
                            .Take(MaxBatchUserIds)
                            .ToList();

            return parsed.Count == 0
                ? Results.BadRequest("Provide at least one valid user id in 'ids'.")
                : Results.Ok(await directory.GetUsersAsync(parsed, ct));
        });

        // The caller's own memberships (which projects/workspaces they may access).
        group.MapGet("/me/memberships", async (ICurrentUser currentUser, IDirectoryService directory, CancellationToken ct) =>
        {
            var memberships = await directory.GetMembershipsAsync(currentUser.Id, ct);
            return memberships is null ? Results.NotFound() : Results.Ok(memberships);
        });

        // All member profiles for a workspace — for HUB sidebar member list and DM initiation.
        group.MapGet("/workspaces/{id:guid}/members", async (Guid id, IDirectoryService directory, CancellationToken ct) =>
            Results.Ok(await directory.GetWorkspaceMembersAsync(id, ct)));

        // Work-item context for linking a discussion thread (Phase 3 integration).
        group.MapGet("/work-items/{id:guid}", async (Guid id, IDirectoryService directory, CancellationToken ct) =>
        {
            var wi = await directory.GetWorkItemAsync(id, ct);
            return wi is null ? Results.NotFound() : Results.Ok(wi);
        });

        // The caller's own display-preference settings (date format, timezone…) sourced from DASHBOARD.
        group.MapGet("/me/settings", async (ICurrentUser currentUser, IDirectoryService directory, CancellationToken ct) =>
        {
            var settings = await directory.GetUserSettingsAsync(currentUser.Id, ct);
            return settings is null ? Results.NotFound() : Results.Ok(settings);
        });

        return app;
    }
}
