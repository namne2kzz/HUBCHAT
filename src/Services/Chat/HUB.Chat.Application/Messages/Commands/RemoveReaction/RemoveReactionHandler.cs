using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Shared.Contracts.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Messages.Commands.RemoveReaction;

/// <summary>Handles <see cref="RemoveReactionCommand"/>.</summary>
/// <param name="db">Chat persistence context.</param>
/// <param name="events">Integration event publisher (transactional outbox).</param>
public sealed class RemoveReactionHandler(IChatDbContext db, IIntegrationEventPublisher events) : IRequestHandler<RemoveReactionCommand>
{
    /// <summary>Removes the caller's reaction if present (idempotent) and announces it.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the reaction is gone.</returns>
    /// <remarks>
    /// Scoped to the caller by construction — the command carries no target user, so nobody can remove
    /// someone else's reaction. Removing a reaction that is not there is a no-op success and publishes
    /// nothing, so a double click or a retry does not push a phantom removal to every open client.
    /// </remarks>
    public async Task Handle(RemoveReactionCommand request, CancellationToken ct)
    {
        var message = await db.Messages
            .Include(m => m.Reactions)
            .FirstOrDefaultAsync(m => m.Id == request.MessageId, ct)
            ?? throw new NotFoundException("Message not found.");

        if (!message.RemoveReaction(request.ActingUserId, request.Emoji))
            return;

        await events.PublishAsync(
            new ReactionRemoved(message.Id, message.ChannelId, request.ActingUserId, request.Emoji), ct);
        await db.SaveChangesAsync(ct);
    }
}
