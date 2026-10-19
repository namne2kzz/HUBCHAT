using HUB.Chat.Application.Channels.Authorization;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Application.Messages.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Messages.Queries.ListChannelAttachments;

/// <summary>Handles <see cref="ListChannelAttachmentsQuery"/>: a channel's attachments for the Files/Media panel.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class ListChannelAttachmentsHandler(IChatDbContext db)
    : IRequestHandler<ListChannelAttachmentsQuery, IReadOnlyList<ChannelAttachmentDto>>
{
    /// <summary>Returns attachments on non-deleted messages, newest first, after checking read access.</summary>
    /// <param name="request">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Attachments with their message context.</returns>
    public async Task<IReadOnlyList<ChannelAttachmentDto>> Handle(ListChannelAttachmentsQuery request, CancellationToken ct)
    {
        await ChannelReadAccess.EnsureCanReadAsync(db, request.ChannelId, request.ActingUserId, ct);

        var limit = Math.Clamp(request.Limit, 1, 100);

        // Attachments live inside the Message aggregate. SelectMany over an owned collection into a DTO
        // ctor doesn't translate across providers, so load the messages that have attachments (newest
        // first, bounded) and flatten in memory. Deleted messages drop their attachments, matching the
        // timeline's tombstone behaviour.
        var messages = await db.Messages.AsNoTracking()
            .Include(m => m.Attachments)
            .Where(m => m.ChannelId == request.ChannelId && m.DeletedAt == null && m.Attachments.Any())
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return [.. messages
            .SelectMany(m => m.Attachments.Select(a => new ChannelAttachmentDto(
                a.Id, m.Id, m.AuthorId, a.Kind, a.Url, a.Name, a.Size, a.Mime, a.Width, a.Height, m.CreatedAt)))
            .Where(a => request.Kind is null || a.Kind == request.Kind)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)];
    }
}
