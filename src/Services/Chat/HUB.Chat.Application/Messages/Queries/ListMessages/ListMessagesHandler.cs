using HUB.Chat.Application.Channels.Authorization;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Application.Common.Models;
using HUB.Chat.Application.Messages.DTOs;
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

        await ChannelReadAccess.EnsureCanReadAsync(db, request.ChannelId, request.ActingUserId, ct);

        var query = db.Messages.AsNoTracking()
            .Include(m => m.Reactions)
            .Include(m => m.Attachments)
            .Where(m => m.ChannelId == request.ChannelId && m.ParentId == null);

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

        // Thread summary (reply count + newest reply time) for this page's messages, in one grouped query —
        // not one per message. Replies are messages whose ParentId is a message on this page.
        var ids = messages.Select(m => m.Id).ToList();
        var threads = await db.Messages.AsNoTracking()
            .Where(r => r.ParentId != null && ids.Contains(r.ParentId.Value) && r.DeletedAt == null)
            .GroupBy(r => r.ParentId!.Value)
            .Select(g => new { ParentId = g.Key, Count = g.Count(), LastReplyAt = (DateTime?)g.Max(r => r.CreatedAt) })
            .ToDictionaryAsync(x => x.ParentId, ct);

        var items = messages.Select(m => threads.TryGetValue(m.Id, out var t)
            ? m.ToDto() with { ReplyCount = t.Count, LastReplyAt = t.LastReplyAt }
            : m.ToDto()).ToList();

        string? next = items.Count == limit
            ? new MessageCursor(items[^1].CreatedAt, items[^1].Id).Encode()
            : null;

        return new CursorPage<MessageDto>(items, next);
    }
}
