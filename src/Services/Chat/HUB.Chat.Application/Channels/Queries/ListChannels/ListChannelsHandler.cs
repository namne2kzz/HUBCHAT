using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Queries.ListChannels;

/// <summary>Handles <see cref="ListChannelsQuery"/> with a no-tracking projection.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class ListChannelsHandler(IChatDbContext db) : IRequestHandler<ListChannelsQuery, IReadOnlyList<ChannelDto>>
{
    /// <summary>Unread counts stop at this — the badge shows "99+" anyway, and counting a huge backlog is wasted work.</summary>
    public const int UnreadCap = 100;

    /// <summary>Returns visible channels ordered by name.</summary>
    /// <param name="request">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The visible channels.</returns>
    public async Task<IReadOnlyList<ChannelDto>> Handle(ListChannelsQuery request, CancellationToken ct)
    {
        var acting = request.ActingUserId;

        return await db.Channels
            .AsNoTracking()
            // Repo channels (public or member) for the requested workspace, PLUS all of the user's
            // DMs regardless of workspace (DMs are org/user-level; the shell dedupes across repos).
            .Where(c =>
                (c.WorkspaceId == request.WorkspaceId
                    && (c.Type == ChannelType.Public || c.Members.Any(m => m.UserId == acting)))
                || ((c.Type == ChannelType.Dm || c.Type == ChannelType.GroupDm)
                    && c.Members.Any(m => m.UserId == acting)))
            .OrderBy(c => c.Name)
            .Select(c => new ChannelDto(
                c.Id, c.WorkspaceId, c.Name, c.Slug, c.Type, c.Topic,
                c.IsPrivate, c.IsArchived, c.Members.Count, c.CreatedAt,
                c.LinkType, c.LinkExternalKey, c.LinkUrl,
                IsMember: c.Members.Any(m => m.UserId == acting),
                OtherUserId: (c.Type == ChannelType.Dm || c.Type == ChannelType.GroupDm)
                    ? c.Members.Where(m => m.UserId != acting).Select(m => (Guid?)m.UserId).FirstOrDefault()
                    : null,
                // Projected here rather than via ToDto: this is a no-tracking LINQ projection, so the
                // role has to be a subquery EF can translate. Without it the sidebar cannot tell a
                // channel the user owns from one they merely joined.
                MyRole: c.Members.Where(m => m.UserId == acting)
                    .Select(m => (ChannelMemberRole?)m.Role).FirstOrDefault(),
                // Others' top-level messages after the caller's read point — or after they joined, if they
                // never read (a newcomer should not see the whole history as unread). Non-members match no
                // member row, so 0. Seeks the (ChannelId, CreatedAt) index; capped so a backlog stays cheap.
                UnreadCount: db.Messages
                    .Where(msg => msg.ChannelId == c.Id && msg.ParentId == null && msg.DeletedAt == null
                               && msg.AuthorId != acting
                               && c.Members.Any(m => m.UserId == acting
                                    && msg.CreatedAt > (m.LastReadAt ?? m.JoinedAt)))
                    .Take(UnreadCap)
                    .Count(),
                // Group DMs are named from their people client-side, so carry the member ids.
                GroupMemberIds: c.Type == ChannelType.GroupDm
                    ? c.Members.Select(m => m.UserId).ToList()
                    : null))
            .ToListAsync(ct);
    }
}
