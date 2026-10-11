using MediatR;

namespace HUB.Chat.Application.Messages.Commands.RemoveReaction;

/// <summary>Removes the acting user's own emoji reaction from a message.</summary>
/// <param name="MessageId">Target message.</param>
/// <param name="Emoji">Emoji to remove.</param>
/// <param name="ActingUserId">User removing their reaction (only their own can be removed).</param>
public sealed record RemoveReactionCommand(Guid MessageId, string Emoji, Guid ActingUserId) : IRequest;
