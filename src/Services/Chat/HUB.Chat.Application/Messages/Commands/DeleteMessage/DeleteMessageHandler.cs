using HUB.Chat.Application.Channels.Authorization;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Shared.Contracts.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Messages.Commands.DeleteMessage;

/// <summary>Handles <see cref="DeleteMessageCommand"/>: author (within the window) or a channel moderator deletes a message.</summary>
/// <param name="db">Chat persistence context.</param>
/// <param name="events">Integration event publisher (transactional outbox).</param>
/// <param name="permissions">Caller workspace permissions (for the moderator path).</param>
public sealed class DeleteMessageHandler(IChatDbContext db, IIntegrationEventPublisher events, IWorkspacePermissions permissions)
    : IRequestHandler<DeleteMessageCommand>
{
    /// <summary>Soft-deletes the message to a tombstone and announces it. Idempotent for an already-deleted message.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the message is deleted.</returns>
    /// <exception cref="NotFoundException">The message does not exist.</exception>
    /// <exception cref="ForbiddenException">The caller is neither the author (in window) nor a moderator.</exception>
    public async Task Handle(DeleteMessageCommand request, CancellationToken ct)
    {
        var message = await db.Messages.FirstOrDefaultAsync(m => m.Id == request.MessageId, ct)
            ?? throw new NotFoundException("Message not found.");

        if (message.IsDeleted) return; // already a tombstone — nothing to do, nothing to re-announce

        var channel = await db.Channels.Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == message.ChannelId, ct)
            ?? throw new NotFoundException("Channel not found.");

        // The author may delete their own within the window; a moderator may delete anyone's, anytime.
        var isAuthorInWindow = message.AuthorId == request.ActingUserId && message.IsWithinAuthorWindow(DateTime.UtcNow);
        if (!isAuthorInWindow && !await ChannelMemberManagement.CanModerateAsync(channel, request.ActingUserId, permissions, ct))
            throw new ForbiddenException("You can only delete your own messages, or you must be a channel moderator.");

        message.SoftDelete();

        await events.PublishAsync(new MessageDeleted(message.Id, message.ChannelId), ct);
        await db.SaveChangesAsync(ct);
    }
}
