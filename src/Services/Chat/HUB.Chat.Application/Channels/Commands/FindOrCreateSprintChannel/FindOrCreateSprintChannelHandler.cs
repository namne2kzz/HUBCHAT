using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.FindOrCreateSprintChannel;

/// <summary>
/// Handles <see cref="FindOrCreateSprintChannelCommand"/>: looks up an existing sprint-linked channel
/// or creates a new Private one; idempotent for the same sprint.
/// </summary>
/// <param name="db">Chat persistence context.</param>
public sealed class FindOrCreateSprintChannelHandler(IChatDbContext db)
    : IRequestHandler<FindOrCreateSprintChannelCommand, ChannelDto>
{
    /// <summary>Finds or creates the sprint channel and returns its DTO.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The existing or newly created <see cref="ChannelDto"/>.</returns>
    public async Task<ChannelDto> Handle(FindOrCreateSprintChannelCommand request, CancellationToken ct)
    {
        // ── Look up existing sprint-linked channel ────────────────────────────
        var existing = await FindAsync(request, ct);

        if (existing is not null)
            return ToDto(existing);

        // ── Create new Private channel ────────────────────────────────────────
        // The name maps to a unique (WorkspaceId, Slug). A different channel may already own the base slug
        // (another sprint with a similar name, or a user-made "sprint-x"), so disambiguate with a numeric
        // suffix instead of failing with a 409 the caller can't act on.
        var rawBase  = $"sprint-{Slugify(request.SprintName)}";
        var baseName = rawBase.Length > 90 ? rawBase[..90].TrimEnd('-') : rawBase; // headroom for a "-NN" suffix

        for (var attempt = 0; attempt <= MaxSlugAttempts; attempt++)
        {
            var channelName = attempt == 0 ? baseName : $"{baseName}-{attempt + 1}";
            var channel = Channel.Create(
                request.WorkspaceId, channelName, ChannelType.Private, request.CreatorUserId,
                topic: $"Discussion channel for sprint: {request.SprintName}");

            channel.LinkTo(
                LinkedResourceType.Sprint, request.SprintId,
                externalKey: request.SprintName,
                url: string.Empty); // DASHBOARD will store the URL; HUB doesn't know the frontend URL

            db.Channels.Add(channel);
            try
            {
                await db.SaveChangesAsync(ct);
                return ToDto(channel);
            }
            catch (UniqueConstraintViolationException)
            {
                db.DiscardChanges();

                // A concurrent call created THIS sprint's channel first (unique link index) — return it.
                var winner = await FindAsync(request, ct);
                if (winner is not null) return ToDto(winner);

                // Otherwise the clash was the (WorkspaceId, Slug) — a name collision. Try the next suffix.
            }
        }

        // Exhausted suffixes: fall back to a slug that cannot collide rather than 409.
        return await CreateWithUniqueSlugAsync(request, baseName, ct);
    }

    /// <summary>Most "-N" suffixes tried before falling back to a guid-suffixed slug.</summary>
    private const int MaxSlugAttempts = 20;

    /// <summary>Last-resort create with a slug made unique by a short random suffix.</summary>
    private async Task<ChannelDto> CreateWithUniqueSlugAsync(
        FindOrCreateSprintChannelCommand request, string baseName, CancellationToken ct)
    {
        var channel = Channel.Create(
            request.WorkspaceId, $"{baseName}-{Guid.NewGuid():N}"[..Math.Min(baseName.Length + 9, 100)],
            ChannelType.Private, request.CreatorUserId,
            topic: $"Discussion channel for sprint: {request.SprintName}");
        channel.LinkTo(LinkedResourceType.Sprint, request.SprintId, externalKey: request.SprintName, url: string.Empty);
        db.Channels.Add(channel);
        await db.SaveChangesAsync(ct);
        return ToDto(channel);
    }

    private Task<Channel?> FindAsync(FindOrCreateSprintChannelCommand request, CancellationToken ct) =>
        db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c =>
                c.WorkspaceId    == request.WorkspaceId  &&
                c.LinkType       == LinkedResourceType.Sprint &&
                c.LinkExternalId == request.SprintId, ct);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ChannelDto ToDto(Channel c) => new(
        c.Id, c.WorkspaceId, c.Name, c.Slug, c.Type, c.Topic,
        c.IsPrivate, c.IsArchived, c.Members.Count, c.CreatedAt,
        c.LinkType, c.LinkExternalKey, c.LinkUrl);

    private static string Slugify(string name)
    {
        var slug = new string(name.Trim().ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }
}
