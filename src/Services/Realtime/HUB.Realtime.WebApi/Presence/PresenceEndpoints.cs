using HUB.Realtime.WebApi.Presence;
using Microsoft.AspNetCore.Mvc;

namespace HUB.Realtime.WebApi.Presence;

/// <summary>Read endpoint for presence status.</summary>
public static class PresenceEndpoints
{
    /// <summary>Maps GET /api/v1/presence?userIds=g1,g2.</summary>
    public static WebApplication MapPresenceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/presence", async (
            [FromQuery] string userIds, IPresenceStore store, CancellationToken ct) =>
        {
            var ids = ParseIds(userIds);
            var statuses = await store.GetStatusesAsync(ids, ct);
            return Results.Ok(statuses.Select(kv => new { userId = kv.Key, status = kv.Value.ToString() }));
        }).RequireAuthorization();

        // Custom statuses (CF-19) — a separate read because they are a separate axis from presence and
        // most presence reads (e.g. the green dot in a long member list) don't need the note.
        app.MapGet("/api/v1/presence/custom", async (
            [FromQuery] string userIds, IPresenceStore store, CancellationToken ct) =>
        {
            var ids = ParseIds(userIds);
            var statuses = await store.GetCustomStatusesAsync(ids, ct);
            return Results.Ok(statuses.Select(kv => new
            {
                userId = kv.Key,
                emoji = kv.Value.Emoji,
                text = kv.Value.Text,
                expiresAt = kv.Value.ExpiresAt?.ToString("o"),
            }));
        }).RequireAuthorization();

        return app;
    }

    /// <summary>Parses a comma-separated list of GUIDs, ignoring blanks and malformed entries.</summary>
    /// <param name="userIds">Raw comma-separated query value.</param>
    /// <returns>The parsed, de-duplicated ids.</returns>
    private static List<Guid> ParseIds(string userIds) =>
        [.. userIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
            .Where(g => g is not null).Select(g => g!.Value).Distinct()];
}
