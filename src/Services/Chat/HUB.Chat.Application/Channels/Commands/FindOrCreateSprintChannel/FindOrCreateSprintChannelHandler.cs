using HUB.Chat.Application.Channels.DTOs;
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
        var existing = await db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c =>
                c.WorkspaceId    == request.WorkspaceId  &&
                c.LinkType       == LinkedResourceType.Sprint &&
                c.LinkExternalId == request.SprintId, ct);

        if (existing is not null)
            return ToDto(existing);

        // ── Create new Private channel ────────────────────────────────────────
        var channelName = $"sprint-{Slugify(request.SprintName)}";
        var channel = Channel.Create(
            request.WorkspaceId,
            channelName,
            ChannelType.Private,
            request.CreatorUserId,
            topic: $"Discussion channel for sprint: {request.SprintName}");

        channel.LinkTo(
            LinkedResourceType.Sprint,
            request.SprintId,
            externalKey: request.SprintName,
            url: string.Empty); // DASHBOARD will store the URL; HUB doesn't know the frontend URL

        db.Channels.Add(channel);
        await db.SaveChangesAsync(ct);

        return ToDto(channel);
    }

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
