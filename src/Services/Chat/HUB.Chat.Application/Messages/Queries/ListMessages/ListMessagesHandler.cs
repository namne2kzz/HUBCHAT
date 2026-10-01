using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Application.Common.Models;
using HUB.Chat.Application.Messages.DTOs;
using HUB.Chat.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Messages.Queries.ListMessages;

/// <summary>Handles <see cref="ListMessagesQuery"/> with keyset (seek) pagination on CreatedAt (desc).</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class ListMessagesHandler(IChatDbContext db) : IRequestHandler<ListMessagesQuery, CursorPage<MessageDto>>
{
    /// <summary>Returns a page of messages plus a cursor for the next older page.</summary>
    /// <param name="request">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A cursor page of messages.</returns>
    public async Task<CursorPage<MessageDto>> Handle(ListMessagesQuery request, CancellationToken ct)
    {
        var limit = Math.Clamp(request.Limit, 1, 100);

        var channel = await db.Channels.AsNoTracking()
            .Where(c => c.Id == request.ChannelId)
            .Select(c => new { c.Type, IsMember = c.Members.Any(m => m.UserId == request.ActingUserId) })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Channel not found.");

        if (channel.Type != ChannelType.Public && !channel.IsMember)
            throw new ForbiddenException("You are not a member of this channel.");

        var query = db.Messages.AsNoTracking()
            .Include(m => m.Reactions)
            .Include(m => m.Attachments)
            .Where(m => m.ChannelId == request.ChannelId && m.ParentId == null && m.DeletedAt == null);

        // Keyset seek: fetch rows strictly older than the cursor, ordered by (CreatedAt desc, Id desc).
        // The Id tie-breaker has to appear here as well as in the ORDER BY — comparing CreatedAt alone
        // skips *every* message sharing the boundary timestamp, not just the boundary row itself, so a
        // bulk insert or import that lands several messages on one tick loses all but the first page's
        // worth. Backed by the (ChannelId, CreatedAt) index.
        if (MessageCursor.TryDecode(request.Cursor) is { } cursor)
            query = query.Where(m => m.CreatedAt < cursor.CreatedAt
                                  || (m.CreatedAt == cursor.CreatedAt && m.Id < cursor.Id));

        var messages = await query
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(limit)
            .ToListAsync(ct);

        var items = messages.Select(m => m.ToDto()).ToList();

        string? next = items.Count == limit
            ? new MessageCursor(items[^1].CreatedAt, items[^1].Id).Encode()
            : null;

        return new CursorPage<MessageDto>(items, next);
    }
}
