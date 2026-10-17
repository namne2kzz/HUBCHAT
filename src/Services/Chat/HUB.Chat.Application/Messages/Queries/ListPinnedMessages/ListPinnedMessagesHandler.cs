using HUB.Chat.Application.Channels.Authorization;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Application.Messages.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Messages.Queries.ListPinnedMessages;

/// <summary>Handles <see cref="ListPinnedMessagesQuery"/>: the channel's pinned messages, newest pin first.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class ListPinnedMessagesHandler(IChatDbContext db) : IRequestHandler<ListPinnedMessagesQuery, IReadOnlyList<MessageDto>>
{
    /// <summary>Returns pinned, non-deleted messages after checking read access.</summary>
    /// <param name="request">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Pinned messages, most recently pinned first.</returns>
    public async Task<IReadOnlyList<MessageDto>> Handle(ListPinnedMessagesQuery request, CancellationToken ct)
    {
        await ChannelReadAccess.EnsureCanReadAsync(db, request.ChannelId, request.ActingUserId, ct);

        var pinned = await db.Messages.AsNoTracking()
            .Include(m => m.Reactions)
            .Include(m => m.Attachments)
            .Where(m => m.ChannelId == request.ChannelId && m.PinnedAt != null && m.DeletedAt == null)
            .OrderByDescending(m => m.PinnedAt)
            .ToListAsync(ct);

        return [.. pinned.Select(m => m.ToDto())];
    }
}
