using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Application.Messages.DTOs;
using HUB.Chat.Domain.Common;
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
    /// <returns>The created message as a DTO — or, for a retried <c>ClientMessageId</c>, the original one.</returns>
    /// <remarks>
    /// Idempotency: a replay is answered from the first send and publishes nothing, so a retry can never
    /// fan out or notify twice. Two concurrent sends with the same key both pass the lookup; the unique
    /// index (AuthorId, ClientMessageId) lets exactly one commit, and the loser's transaction — outbox rows
    /// included — rolls back before it re-reads the winner.
    /// </remarks>
    public async Task<MessageDto> Handle(PostMessageCommand request, CancellationToken ct)
    {
        if (request.ClientMessageId is { } key && await FindReplayAsync(request, key, ct) is { } replay)
            return replay;

        var channel = await db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, ct)
            ?? throw new NotFoundException("Channel not found.");

        if (!channel.HasMember(request.ActingUserId))
            throw new ForbiddenException("You are not a member of this channel.");

        channel.EnsureWritable();

        var message = Message.Post(
            request.ChannelId, request.ActingUserId, request.Body, request.Format,
            parentId: request.ParentId, mentions: request.MentionedUserIds,
            clientMessageId: request.ClientMessageId);

        db.Messages.Add(message);

        // Publish through the outbox — stored in the same transaction, delivered after SaveChanges.
        await events.PublishAsync(
            new MessageSent(
                message.Id, channel.Id, message.AuthorId, Preview(message.Body), message.Mentions.ToList(),
                Body: message.Body, Format: (int)message.Format, ParentId: message.ParentId, CreatedAt: message.CreatedAt),
            ct);

        foreach (var mentioned in message.Mentions)
            await events.PublishAsync(
                new UserMentioned(mentioned, channel.Id, message.Id, message.AuthorId, Preview(message.Body)), ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException) when (request.ClientMessageId is { } raceKey)
        {
            // Lost the race to a concurrent send with the same key — answer with the winner's message.
            // The losing message and its outbox rows were rolled back; stop tracking them too.
            db.DiscardChanges();
            return await FindReplayAsync(request, raceKey, ct) ?? throw new InvalidOperationException(
                "Unique violation on send, but no message exists for the client key.");
        }

        return message.ToDto();
    }

    /// <summary>Returns the author's message already stored under <paramref name="key"/>, or null.</summary>
    /// <exception cref="DomainException">The key was already used for a message in another channel.</exception>
    private async Task<MessageDto?> FindReplayAsync(PostMessageCommand request, Guid key, CancellationToken ct)
    {
        var existing = await db.Messages
            .AsNoTracking()
            .Include(m => m.Reactions)
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.AuthorId == request.ActingUserId && m.ClientMessageId == key, ct);

        if (existing is null) return null;

        // Same key, different target: a client bug, not a retry. Refuse rather than hand back an
        // unrelated message as if this send had succeeded.
        if (existing.ChannelId != request.ChannelId)
            throw new DomainException("This client message id was already used for a message in another channel.");

        return existing.ToDto();
    }

    private static string Preview(string body) => body.Length <= 140 ? body : body[..140];
}
