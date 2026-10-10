using HUB.Chat.Application.Channels.Authorization;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Application.Messages.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Messages.Queries.ListMessagesAfter;

/// <summary>Handles <see cref="ListMessagesAfterQuery"/> with keyset seek forward on (CreatedAt, Id).</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class ListMessagesAfterHandler(IChatDbContext db) : IRequestHandler<ListMessagesAfterQuery, IReadOnlyList<MessageDto>>
{
    /// <summary>Returns up to <c>Limit</c> messages strictly newer than the anchor, oldest first.</summary>
    /// <param name="request">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The messages after the anchor; empty when the client is up to date.</returns>
    /// <exception cref="NotFoundException">The anchor message is not in this channel — the client should reload instead.</exception>
    /// <remarks>
    /// Mirror image of <c>ListMessagesHandler</c>'s backward seek, with the same (CreatedAt, Id) tie-breaker so
    /// messages sharing a timestamp are neither skipped nor repeated. The client pages by passing the last
    /// returned id as the next anchor until it gets fewer than <c>Limit</c> items.
    /// </remarks>
    public async Task<IReadOnlyList<MessageDto>> Handle(ListMessagesAfterQuery request, CancellationToken ct)
    {
        var limit = Math.Clamp(request.Limit, 1, 100);

        await ChannelReadAccess.EnsureCanReadAsync(db, request.ChannelId, request.ActingUserId, ct);

        // Scoped to the channel: an anchor from another channel must not leak that channel's timeline position.
        var anchor = await db.Messages.AsNoTracking()
            .Where(m => m.Id == request.AfterMessageId && m.ChannelId == request.ChannelId)
            .Select(m => new { m.CreatedAt, m.Id })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Anchor message not found in this channel.");

        var messages = await db.Messages.AsNoTracking()
            .Include(m => m.Reactions)
            .Include(m => m.Attachments)
            .Where(m => m.ChannelId == request.ChannelId && m.ParentId == null && m.DeletedAt == null)
            .Where(m => m.CreatedAt > anchor.CreatedAt
                     || (m.CreatedAt == anchor.CreatedAt && m.Id > anchor.Id))
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Take(limit)
            .ToListAsync(ct);

        return [.. messages.Select(m => m.ToDto())];
    }
}
