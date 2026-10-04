using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Shared.Contracts.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Messages.Commands.AddReaction;

/// <summary>Handles <see cref="AddReactionCommand"/>.</summary>
/// <param name="db">Chat persistence context.</param>
/// <param name="events">Integration event publisher (transactional outbox).</param>
public sealed class AddReactionHandler(IChatDbContext db, IIntegrationEventPublisher events) : IRequestHandler<AddReactionCommand>
{
    /// <summary>Loads the message, verifies channel membership, and adds the reaction (idempotent per user+emoji).</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the reaction is stored.</returns>
    /// <remarks>
    /// Publishes <see cref="ReactionAdded"/> through the outbox only when the reaction is new, so a repeated
    /// click does not push a duplicate to every open client.
    /// </remarks>
    public async Task Handle(AddReactionCommand request, CancellationToken ct)
    {
        var message = await db.Messages
            .Include(m => m.Reactions)
            .FirstOrDefaultAsync(m => m.Id == request.MessageId, ct)
            ?? throw new NotFoundException("Message not found.");

        var isMember = await db.Channels.AsNoTracking()
            .AnyAsync(c => c.Id == message.ChannelId && c.Members.Any(m => m.UserId == request.ActingUserId), ct);
        if (!isMember)
            throw new ForbiddenException("You are not a member of this channel.");

        if (!message.AddReaction(request.ActingUserId, request.Emoji))
            return;

        await events.PublishAsync(
            new ReactionAdded(message.Id, message.ChannelId, request.ActingUserId, request.Emoji), ct);
        await db.SaveChangesAsync(ct);
    }
}
