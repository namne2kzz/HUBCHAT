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
            var ids = userIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
                .Where(g => g is not null).Select(g => g!.Value).ToList();

            var statuses = await store.GetStatusesAsync(ids, ct);
            return Results.Ok(statuses.Select(kv => new { userId = kv.Key, status = kv.Value.ToString() }));
        }).RequireAuthorization();

        return app;
    }
}
