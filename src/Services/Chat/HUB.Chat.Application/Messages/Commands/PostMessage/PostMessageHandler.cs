using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Application.Messages.DTOs;
using HUB.Chat.Domain.Entities;
using HUB.Shared.Contracts.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Messages.Commands.PostMessage;

/// <summary>Handles <see cref="PostMessageCommand"/>: persists the message and publishes integration events via the outbox.</summary>
/// <param name="db">Chat persistence context.</param>
/// <param name="events">Integration event publisher (transactional outbox).</param>
public sealed class PostMessageHandler(IChatDbContext db, IIntegrationEventPublisher events)
    : IRequestHandler<PostMessageCommand, MessageDto>
{
    /// <summary>Validates membership, saves the message, and publishes MessageSent + UserMentioned atomically.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created message as a DTO.</returns>
    public async Task<MessageDto> Handle(PostMessageCommand request, CancellationToken ct)
    {
        var channel = await db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, ct)
            ?? throw new NotFoundException("Channel not found.");

        if (!channel.HasMember(request.ActingUserId))
            throw new ForbiddenException("You are not a member of this channel.");

        channel.EnsureWritable();

        var message = Message.Post(
            request.ChannelId, request.ActingUserId, request.Body, request.Format,
            parentId: request.ParentId, mentions: request.MentionedUserIds);

        db.Messages.Add(message);

        // Publish through the outbox — stored in the same transaction, delivered after SaveChanges.
        await events.PublishAsync(
            new MessageSent(message.Id, channel.Id, message.AuthorId, Preview(message.Body), message.Mentions.ToList()),
            ct);

        foreach (var mentioned in message.Mentions)
            await events.PublishAsync(new UserMentioned(mentioned, channel.Id, message.Id, message.AuthorId), ct);

        await db.SaveChangesAsync(ct);

        return message.ToDto();
    }

    private static string Preview(string body) => body.Length <= 140 ? body : body[..140];
}
