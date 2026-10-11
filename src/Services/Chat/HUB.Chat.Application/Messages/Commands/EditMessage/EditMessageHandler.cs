using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Application.Messages.DTOs;
using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Entities;
using HUB.Shared.Contracts.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Messages.Commands.EditMessage;

/// <summary>Handles <see cref="EditMessageCommand"/>: the author edits their own message within the edit window.</summary>
/// <param name="db">Chat persistence context.</param>
/// <param name="events">Integration event publisher (transactional outbox).</param>
public sealed class EditMessageHandler(IChatDbContext db, IIntegrationEventPublisher events)
    : IRequestHandler<EditMessageCommand, MessageDto>
{
    /// <summary>Applies the edit and announces it so open clients update the body in place.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The edited message as a DTO.</returns>
    /// <exception cref="NotFoundException">The message does not exist.</exception>
    /// <exception cref="ForbiddenException">The caller is not the author.</exception>
    /// <exception cref="DomainException">The edit window has passed, or the message is deleted/empty.</exception>
    public async Task<MessageDto> Handle(EditMessageCommand request, CancellationToken ct)
    {
        var message = await db.Messages
            .Include(m => m.Reactions)
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == request.MessageId, ct)
            ?? throw new NotFoundException("Message not found.");

        // Editing changes meaning, so unlike delete it is author-only — a moderator may remove, not reword.
        if (message.AuthorId != request.ActingUserId)
            throw new ForbiddenException("You can only edit your own messages.");
        if (!message.IsWithinAuthorWindow(DateTime.UtcNow))
            throw new DomainException("This message is too old to edit.");

        message.Edit(request.Body); // throws if deleted or empty

        await events.PublishAsync(new MessageEdited(message.Id, message.ChannelId, message.Body, message.EditedAt!.Value), ct);
        await db.SaveChangesAsync(ct);

        return message.ToDto();
    }
}
