using HUB.Chat.Application.Channels.Authorization;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Application.Messages.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Messages.Queries.ListThreadReplies;

/// <summary>Handles <see cref="ListThreadRepliesQuery"/>: the replies under one parent message, oldest first.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class ListThreadRepliesHandler(IChatDbContext db) : IRequestHandler<ListThreadRepliesQuery, IReadOnlyList<MessageDto>>
{
    /// <summary>Returns the thread's replies (deleted ones kept as tombstones, matching the main list).</summary>
    /// <param name="request">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Replies oldest→newest.</returns>
    /// <exception cref="NotFoundException">The parent message is not in this channel.</exception>
    public async Task<IReadOnlyList<MessageDto>> Handle(ListThreadRepliesQuery request, CancellationToken ct)
    {
        await ChannelReadAccess.EnsureCanReadAsync(db, request.ChannelId, request.ActingUserId, ct);

        // Scope the parent to the channel so a reply list cannot be fetched via a foreign channel id.
        var parentExists = await db.Messages.AsNoTracking()
            .AnyAsync(m => m.Id == request.ParentId && m.ChannelId == request.ChannelId, ct);
        if (!parentExists) throw new NotFoundException("Thread not found in this channel.");

        var replies = await db.Messages.AsNoTracking()
            .Include(m => m.Reactions)
            .Include(m => m.Attachments)
            .Where(m => m.ParentId == request.ParentId)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .ToListAsync(ct);

        return [.. replies.Select(m => m.ToDto())];
    }
}
