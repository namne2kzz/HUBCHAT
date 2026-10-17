using HUB.Chat.Application.Channels.Authorization;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Application.Messages.DTOs;
using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Entities;
using HUB.Shared.Contracts.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Messages.Commands.ForwardMessage;

/// <summary>Handles <see cref="ForwardMessageCommand"/>: copies a readable message into a channel the caller belongs to.</summary>
/// <param name="db">Chat persistence context.</param>
/// <param name="events">Integration event publisher (transactional outbox).</param>
public sealed class ForwardMessageHandler(IChatDbContext db, IIntegrationEventPublisher events)
    : IRequestHandler<ForwardMessageCommand, MessageDto>
{
    /// <summary>Validates read-at-source + write-at-target, copies the body, and publishes the new message.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The forwarded message in the target channel.</returns>
    /// <exception cref="NotFoundException">Source message or target channel missing.</exception>
    /// <exception cref="ForbiddenException">Caller cannot read the source, or is not a member of the target.</exception>
    /// <exception cref="DomainException">The source is deleted (nothing to forward) or the target is archived.</exception>
    public async Task<MessageDto> Handle(ForwardMessageCommand request, CancellationToken ct)
    {
        var source = await db.Messages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.SourceMessageId, ct)
                     ?? throw new NotFoundException("Message to forward not found.");
        if (source.IsDeleted) throw new DomainException("Cannot forward a deleted message.");

        // Must be allowed to see the source before copying it out of its channel.
        await ChannelReadAccess.EnsureCanReadAsync(db, source.ChannelId, request.ActingUserId, ct);

        var target = await db.Channels.Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == request.TargetChannelId, ct)
            ?? throw new NotFoundException("Target channel not found.");
        if (!target.HasMember(request.ActingUserId))
            throw new ForbiddenException("You are not a member of the target channel.");
        target.EnsureWritable();

        var message = Message.Forward(target.Id, request.ActingUserId, source.Body, source.Format, source.Id);
        db.Messages.Add(message);

        await events.PublishAsync(
            new MessageSent(message.Id, target.Id, message.AuthorId, Preview(message.Body), [],
                Body: message.Body, Format: (int)message.Format, ParentId: null, CreatedAt: message.CreatedAt),
            ct);
        await db.SaveChangesAsync(ct);

        return message.ToDto();
    }

    private static string Preview(string body) => body.Length <= 140 ? body : body[..140];
}
