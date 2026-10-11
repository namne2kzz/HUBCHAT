using MediatR;

namespace HUB.Chat.Application.Messages.Commands.DeleteMessage;

/// <summary>Deletes a message (soft-delete to a tombstone).</summary>
/// <param name="MessageId">Message to delete.</param>
/// <param name="ActingUserId">The user deleting — author (within the window) or a channel moderator.</param>
public sealed record DeleteMessageCommand(Guid MessageId, Guid ActingUserId) : IRequest;
